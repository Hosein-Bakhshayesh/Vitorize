using System.Collections.Concurrent;
using System.Net.Http.Json;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;

namespace Vitorize.Web.Services.Auth;

public sealed record RefreshResult(bool Success, string? AccessToken = null, string? RefreshToken = null);

/// <summary>Coalesces rotation for a single browser session without ever recording token values.</summary>
public sealed class SessionTokenRefreshCoordinator
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> Pending = new(StringComparer.Ordinal);
    private readonly HttpClient _http;

    public SessionTokenRefreshCoordinator(HttpClient http) => _http = http;

    public async Task<RefreshResult> RefreshAsync(string scheme, string refreshToken, CancellationToken cancellationToken)
    {
        if (scheme is not (VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme) || string.IsNullOrWhiteSpace(refreshToken))
            return new RefreshResult(false);

        var key = scheme + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));
        var created = new Lazy<Task<RefreshResult>>(() => RefreshCoreAsync(refreshToken), LazyThreadSafetyMode.ExecutionAndPublication);
        var entry = Pending.GetOrAdd(key, created);
        if (ReferenceEquals(entry, created))
        {
            _ = RetireAsync(key, entry);
        }
        return await entry.Value.WaitAsync(cancellationToken);
    }

    private static async Task RetireAsync(string key, Lazy<Task<RefreshResult>> entry)
    {
        try { await entry.Value; }
        catch { /* RefreshCore converts transport failures into a safe result. */ }
        await Task.Delay(TimeSpan.FromSeconds(30));
        Pending.TryRemove(new KeyValuePair<string, Lazy<Task<RefreshResult>>>(key, entry));
    }

    private async Task<RefreshResult> RefreshCoreAsync(string refreshToken)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("auth/refresh-token", new { RefreshToken = refreshToken }, CancellationToken.None);
            if (!response.IsSuccessStatusCode) return new RefreshResult(false);
            var result = await response.Content.ReadFromJsonAsync<ApiResult<AdminLoginResponseModel>>();
            var data = result?.Data;
            var access = data?.GetAccessToken();
            return result?.IsSuccess == true && !string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(data?.RefreshToken)
                ? new RefreshResult(true, access, data.RefreshToken) : new RefreshResult(false);
        }
        catch { return new RefreshResult(false); }
    }
}
