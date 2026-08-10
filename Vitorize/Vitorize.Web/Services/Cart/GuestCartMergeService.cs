using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vitorize.Shared.Common;

namespace Vitorize.Web.Services.Cart;

/// <summary>Performs the authenticated, server-side guest-cart merge without exposing the capability to JavaScript.</summary>
public sealed class GuestCartMergeService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GuestCartMergeService> _logger;

    public GuestCartMergeService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<GuestCartMergeService> logger) =>
        (_httpClientFactory, _configuration, _logger) = (httpClientFactory, configuration, logger);

    public async Task<bool> MergeAsync(string? guestToken, string accessToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(guestToken)) return true;
        try
        {
            var baseUrl = _configuration["ApiSettings:BaseUrl"] ?? throw new InvalidOperationException("ApiSettings:BaseUrl is required.");
            using var client = _httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl), "cart/merge-guest"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            request.Content = new StringContent(JsonSerializer.Serialize(new { GuestToken = guestToken }), Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode) return true;
            _logger.LogWarning("GuestCartMergeFailed StatusCode={StatusCode} EventType={EventType}", (int)response.StatusCode, "GuestCartMergeFailed");
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "GuestCartMergeFailed EventType={EventType}", "GuestCartMergeFailed");
            return false;
        }
    }
}
