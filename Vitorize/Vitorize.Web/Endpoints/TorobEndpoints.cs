using System.Net.Http.Headers;
using System.Text.Json;
using Vitorize.Application.DTOs.Torob;

namespace Vitorize.Web.Endpoints;

/// <summary>
/// Publishes the exact Torob URL on the storefront host while keeping the catalogue/data API on its
/// separate host. The proxy passes Torob's signed headers through unchanged and never uses browser
/// session cookies.
/// </summary>
public static class TorobEndpoints
{
    private const string Path = "/api/v1/thirdparties/torob/products";

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
        const int maxRequestBytes = 64 * 1024;
        if (context.Request.ContentLength is > maxRequestBytes)
        {
            await WriteErrorAsync(context, StatusCodes.Status400BadRequest, "بدنه درخواست بیش از حد بزرگ است.", cancellationToken);
            return;
        }

        var apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            await WriteErrorAsync(context, StatusCodes.Status502BadGateway, "مسیر API فروشگاه تنظیم نشده است.", cancellationToken);
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{apiBaseUrl}/v1/thirdparties/torob/products")
        {
            Content = new StreamContent(context.Request.Body)
        };
        if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            request.Content.Headers.TryAddWithoutValidation("Content-Type", context.Request.ContentType);

        CopyHeader("X-Torob-Token");
        CopyHeader("X-Torob-Token-Version");

        try
        {
            using var response = await httpClientFactory.CreateClient("TorobProxy").SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.Headers.CacheControl = "no-store";
            context.Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
            if (response.Content.Headers.ContentLength is long length)
                context.Response.ContentLength = length;
            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The crawler disconnected. There is no response left to write.
        }
        catch (HttpRequestException exception)
        {
            loggerFactory.CreateLogger("TorobProxy").LogError(exception, "Torob catalogue proxy could not reach the API.");
            await WriteErrorAsync(context, StatusCodes.Status502BadGateway, "دریافت کاتالوگ فروشگاه موقتاً ممکن نیست.", cancellationToken);
        }

        void CopyHeader(string headerName)
        {
            if (context.Request.Headers.TryGetValue(headerName, out var values))
                request.Headers.TryAddWithoutValidation(headerName, values.AsEnumerable());
        }
    }

    private static Task WriteErrorAsync(HttpContext context, int statusCode, string error, CancellationToken cancellationToken)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        context.Response.Headers.CacheControl = "no-store";
        return JsonSerializer.SerializeAsync(context.Response.Body, new TorobErrorResponse { Error = error }, cancellationToken: cancellationToken);
    }
}
