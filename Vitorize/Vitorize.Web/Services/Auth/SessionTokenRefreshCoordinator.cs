using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;

namespace Vitorize.Web.Services.Auth;

/// <summary>
/// Why a rotation attempt ended. The distinction matters more than it looks: only
/// <see cref="Rejected"/> is evidence that the browser's session is actually finished. Treating a
/// timeout, a recycling API or a bad gateway as proof of that is what signed customers out mid-visit
/// and left them clearing cookies to recover.
/// </summary>
public enum RefreshOutcome
{
    /// <summary>New tokens were issued.</summary>
    Success = 1,

    /// <summary>The provider authoritatively refused the refresh token: expired, revoked or unknown.</summary>
    Rejected = 2,

    /// <summary>Nothing was learned — network failure, timeout, or a server-side fault. Try again later.</summary>
    Transient = 3
}

public sealed record RefreshResult(RefreshOutcome Outcome, string? AccessToken = null, string? RefreshToken = null)
{
    public bool Success => Outcome == RefreshOutcome.Success;

    public static readonly RefreshResult Rejected = new(RefreshOutcome.Rejected);
    public static readonly RefreshResult Transient = new(RefreshOutcome.Transient);
}

/// <summary>Coalesces rotation for a single browser session without ever recording token values.</summary>
public sealed class SessionTokenRefreshCoordinator
{
    private static readonly ConcurrentDictionary<string, Lazy<Task<RefreshResult>>> Pending = new(StringComparer.Ordinal);
    private readonly HttpClient _http;
    private readonly ILogger<SessionTokenRefreshCoordinator> _logger;

    public SessionTokenRefreshCoordinator(HttpClient http, ILogger<SessionTokenRefreshCoordinator>? logger = null)
    {
        _http = http;
        _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<SessionTokenRefreshCoordinator>.Instance;
    }

    public async Task<RefreshResult> RefreshAsync(string scheme, string refreshToken, CancellationToken cancellationToken)
    {
        if (scheme is not (VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme) || string.IsNullOrWhiteSpace(refreshToken))
            return RefreshResult.Rejected;

        var key = scheme + ":" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));
        var created = new Lazy<Task<RefreshResult>>(() => RefreshCoreAsync(refreshToken), LazyThreadSafetyMode.ExecutionAndPublication);
        var entry = Pending.GetOrAdd(key, created);
        if (ReferenceEquals(entry, created))
        {
            _ = RetireAsync(key, entry);
        }
        return await entry.Value.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// Keeps a decided result briefly so the requests that piled up behind one 401 all reuse it, then
    /// forgets it. A transient result is dropped immediately instead: caching "we could not reach the
    /// API" would turn one network blip into thirty seconds of guaranteed failure for every request.
    /// </summary>
    private static async Task RetireAsync(string key, Lazy<Task<RefreshResult>> entry)
    {
        RefreshResult? result = null;
        try { result = await entry.Value; }
        catch { /* RefreshCore converts transport failures into a safe result. */ }

        if (result is null || result.Outcome == RefreshOutcome.Transient)
        {
            Pending.TryRemove(new KeyValuePair<string, Lazy<Task<RefreshResult>>>(key, entry));
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(30));
        Pending.TryRemove(new KeyValuePair<string, Lazy<Task<RefreshResult>>>(key, entry));
    }

    private async Task<RefreshResult> RefreshCoreAsync(string refreshToken)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("auth/refresh-token", new { RefreshToken = refreshToken }, CancellationToken.None);

            // 401/403 is the provider saying the token itself is finished. Anything else that is not a
            // success says nothing about the session: a recycling app pool, a proxy error or a
            // gateway timeout must not end it.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return RefreshResult.Rejected;

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Token rotation could not be completed. StatusCode={StatusCode} EventType={EventType}",
                    (int)response.StatusCode, "TokenRefreshTransientFailure");
                return RefreshResult.Transient;
            }

            var result = await response.Content.ReadFromJsonAsync<ApiResult<AdminLoginResponseModel>>();
            var data = result?.Data;
            var access = data?.GetAccessToken();
            if (result?.IsSuccess == true && !string.IsNullOrWhiteSpace(access) && !string.IsNullOrWhiteSpace(data?.RefreshToken))
                return new RefreshResult(RefreshOutcome.Success, access, data.RefreshToken);

            // A 200 that carries no usable pair is the provider declining the rotation.
            return RefreshResult.Rejected;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Token rotation failed in transport. ExceptionType={ExceptionType} EventType={EventType}",
                exception.GetType().Name, "TokenRefreshTransientFailure");
            return RefreshResult.Transient;
        }
    }
}
