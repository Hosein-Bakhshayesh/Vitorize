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
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        CancellationToken cancellationToken)
    {
        var token = await accessTokens.GetAccessTokenAsync();
        if (string.IsNullOrWhiteSpace(token))
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

        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"{apiBaseUrl}/verification/documents/{documentId:D}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClientFactory.CreateClient().SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

        context.Response.StatusCode = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
            return;

        context.Response.ContentType = response.Content.Headers.ContentType?.ToString()
            ?? "application/octet-stream";
        context.Response.Headers.CacheControl = "no-store, private";
        if (response.Content.Headers.ContentLength is long length)
            context.Response.ContentLength = length;
        await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
    }
}
