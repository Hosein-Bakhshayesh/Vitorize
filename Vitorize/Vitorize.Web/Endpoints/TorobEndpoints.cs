using System.Text.Json;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Shared.Logging;

namespace Vitorize.Web.Endpoints;

/// <summary>
/// Publishes the exact Torob URL on the storefront host while keeping the catalogue/data API on its
/// separate host. The proxy passes Torob's request headers through unchanged, adds standard forwarding
/// headers so the API can log the crawler's real address and host, and never uses browser session cookies.
/// </summary>
public static class TorobEndpoints
{
    private const string Path = "/api/v1/thirdparties/torob/products";
    private const string EventType = "TorobProxyRequest";
    private static readonly string[] PassThroughHeaders =
    [
        "X-Torob-Token", "C-Torob-Token-Version", "X-Torob-Token-Version", "Accept", "User-Agent"
    ];

    public static IEndpointRouteBuilder MapTorobEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(Path, ProxyProductsAsync)
            .AllowAnonymous()
            .DisableAntiforgery();
        return endpoints;
    }

    private static async Task ProxyProductsAsync(
        HttpContext context,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("TorobProxy");
        var clientAddress = ClientAddress(context);
        var statusCode = 0;
        try
        {
            var apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                statusCode = StatusCodes.Status502BadGateway;
                await WriteErrorAsync(context, statusCode, "مسیر API فروشگاه تنظیم نشده است.", cancellationToken);
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/v1/thirdparties/torob/products")
            {
                Content = new StreamContent(context.Request.Body)
            };
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
                request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);
            request.Content.Headers.ContentLength = context.Request.ContentLength;

            foreach (var name in PassThroughHeaders)
            {
                if (context.Request.Headers.TryGetValue(name, out var values))
                    request.Headers.TryAddWithoutValidation(name, values.AsEnumerable());
            }

            // Standard forwarding headers. The API consumes them only when this host is one of its trusted
            // proxies; it logs the raw values either way, so the crawler's address is always visible there.
            var forwardedFor = string.Join(", ", new[] { context.Request.Headers["X-Forwarded-For"].ToString(), clientAddress }
                .Where(value => !string.IsNullOrWhiteSpace(value)));
            if (forwardedFor.Length > 0)
                request.Headers.TryAddWithoutValidation("X-Forwarded-For", forwardedFor);
            if (context.Request.Host.HasValue)
                request.Headers.TryAddWithoutValidation("X-Forwarded-Host", context.Request.Host.Value);
            request.Headers.TryAddWithoutValidation("X-Forwarded-Proto", context.Request.Scheme);
            if (context.Items.TryGetValue(CorrelationIdPolicy.HeaderName, out var correlation) &&
                correlation is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
                request.Headers.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, correlationId);

            using var response = await httpClientFactory.CreateClient("TorobProxy").SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            statusCode = (int)response.StatusCode;
            context.Response.StatusCode = statusCode;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            if (response.Content.Headers.ContentLength is long length)
                context.Response.ContentLength = length;
            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The crawler disconnected. There is no response left to write.
            statusCode = 499;
        }
        catch (OperationCanceledException exception)
        {
            // The upstream call hit the TorobProxy client timeout while the crawler is still waiting.
            statusCode = StatusCodes.Status504GatewayTimeout;
            logger.LogError(exception, "Torob catalogue proxy timed out waiting for the API. EventType={EventType}", "TorobProxyTimeout");
            await WriteErrorAsync(context, statusCode, "پاسخ کاتالوگ فروشگاه در زمان مجاز دریافت نشد.", cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            statusCode = StatusCodes.Status502BadGateway;
            logger.LogError(exception, "Torob catalogue proxy could not reach the API. EventType={EventType}", "TorobProxyUnreachable");
            await WriteErrorAsync(context, statusCode, "دریافت کاتالوگ فروشگاه موقتاً ممکن نیست.", cancellationToken);
        }
        finally
        {
            var level = statusCode == StatusCodes.Status200OK ? LogLevel.Information : LogLevel.Warning;
            logger.Log(level,
                "Torob proxy request responded {StatusCode}. RemoteIp={RemoteIp} Host={HostHeader} UserAgent={UserAgent} " +
                "ContentType={ContentType} ContentLength={ContentLength} HeaderNames={HeaderNames} AuthHeaderPresent={AuthHeaderPresent} EventType={EventType}",
                statusCode,
                clientAddress,
                context.Request.Host.Value,
                context.Request.Headers.UserAgent.ToString(),
                context.Request.ContentType,
                context.Request.ContentLength,
                context.Request.Headers.Keys.ToArray(),
                context.Request.Headers.ContainsKey("X-Torob-Token"),
                EventType);
        }
    }

    private static string? ClientAddress(HttpContext context)
    {
        var address = context.Connection.RemoteIpAddress;
        if (address is null) return null;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return address.ToString();
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return JsonSerializer.SerializeAsync(context.Response.Body, new TorobErrorResponse { Error = error }, cancellationToken: cancellationToken);
    }
}
