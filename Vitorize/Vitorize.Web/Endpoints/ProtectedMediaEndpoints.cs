using System.Net.Http.Headers;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints;

/// <summary>Streams private verification documents through the authenticated Web host.</summary>
public static class ProtectedMediaEndpoints
{
    public static IEndpointRouteBuilder MapProtectedMediaEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/media/verification-documents/{documentId:guid}", StreamVerificationDocumentAsync)
            .RequireAuthorization();
        return endpoints;
    }

    private static async Task StreamVerificationDocumentAsync(
        Guid documentId,
        HttpContext context,
        IAccessTokenProvider accessTokens,
        SessionTokenRefreshCoordinator refreshCoordinator,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        // This proxy serves TWO audiences on one shared path: the customer viewing their own
        // document, and an administrator reviewing it. The path-based resolver prefers the admin
        // session on /media, so a browser holding BOTH cookies used to present the ADMIN token for
        // the CUSTOMER'S own preview - and the API correctly answered 404 (not the owner). Try each
        // authenticated session's token instead: the customer first (ownership), then the admin
        // (review permission), then the token resolved for the current request.
        var candidates = new[]
        {
            new TokenCandidate(
                VitorizeAuthSchemes.CustomerScheme,
                context.Request.Cookies[VitorizeAuthSchemes.CustomerAccessTokenCookie],
                context.Request.Cookies[VitorizeAuthSchemes.CustomerRefreshTokenCookie]),
            new TokenCandidate(
                VitorizeAuthSchemes.AdminScheme,
                context.Request.Cookies[VitorizeAuthSchemes.AdminAccessTokenCookie],
                context.Request.Cookies[VitorizeAuthSchemes.AdminRefreshTokenCookie]),
            new TokenCandidate(null, await accessTokens.GetAccessTokenAsync(), null)
        }
        .Where(candidate => !string.IsNullOrWhiteSpace(candidate.AccessToken))
        .GroupBy(candidate => candidate.AccessToken!, StringComparer.Ordinal)
        .Select(group => group.First())
            .ToList();
        if (candidates.Count == 0)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var apiBaseUrl = (configuration["ApiSettings:BaseUrl"] ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
        {
            context.Response.StatusCode = StatusCodes.Status502BadGateway;
            return;
        }

        HttpResponseMessage? response = null;
        try
        {
            foreach (var candidate in candidates)
            {
                response?.Dispose();
                response = await FetchAsync(candidate.AccessToken!);
                if (response.IsSuccessStatusCode) break;

                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized ||
                    candidate.Scheme is null || string.IsNullOrWhiteSpace(candidate.RefreshToken))
                    continue;

                var refreshed = await refreshCoordinator.RefreshAsync(candidate.Scheme, candidate.RefreshToken, cancellationToken);
                if (!refreshed.Success || string.IsNullOrWhiteSpace(refreshed.AccessToken) || string.IsNullOrWhiteSpace(refreshed.RefreshToken))
                    continue;

                await AuthSessionCookieWriter.PersistAsync(context, candidate.Scheme, refreshed.AccessToken, refreshed.RefreshToken);
                response.Dispose();
                response = await FetchAsync(refreshed.AccessToken);
                if (response.IsSuccessStatusCode) break;
            }

            context.Response.StatusCode = (int)response!.StatusCode;
            if (!response.IsSuccessStatusCode)
                return;

            context.Response.ContentType = response.Content.Headers.ContentType?.ToString()
                ?? "application/octet-stream";
            context.Response.Headers.CacheControl = "no-store, private";
            if (response.Content.Headers.ContentLength is long length)
                context.Response.ContentLength = length;
            await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
        }
        finally
        {
            response?.Dispose();
        }

        async Task<HttpResponseMessage> FetchAsync(string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{apiBaseUrl}/verification/documents/{documentId:D}/content");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            return await httpClientFactory.CreateClient().SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
    }

    private sealed record TokenCandidate(string? Scheme, string? AccessToken, string? RefreshToken);
}
