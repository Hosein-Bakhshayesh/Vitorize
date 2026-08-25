using Microsoft.Extensions.Caching.Memory;

namespace Vitorize.Web.Services.Auth;

/// <summary>
/// In-process implementation of <see cref="ITokenRotationHandoff"/>.
///
/// The lifetime is deliberately short and matched to the API's rotation grace window: past it, the
/// old refresh token is a replay rather than a handover, and the correct outcome is a clean sign-in
/// rather than a resurrected session. Entries are removed on claim so a pair is adopted exactly once.
/// </summary>
public sealed class TokenRotationHandoff : ITokenRotationHandoff
{
    /// <summary>Matches the API's rotation grace window, so both sides agree during the handover.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(60);

    private const string KeyPrefix = "token-handoff:";
    private readonly IMemoryCache _cache;

    public TokenRotationHandoff(IMemoryCache cache) => _cache = cache;

    public void Remember(string oldRefreshToken, string scheme, string accessToken, string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(oldRefreshToken) ||
            string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(refreshToken))
            return;

        _cache.Set(
            Key(oldRefreshToken),
            new RotatedTokens(scheme, accessToken, refreshToken),
            new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Lifetime });
    }

    public bool TryTake(string oldRefreshToken, out RotatedTokens? tokens)
    {
        tokens = null;
        if (string.IsNullOrWhiteSpace(oldRefreshToken)) return false;

        var key = Key(oldRefreshToken);
        if (!_cache.TryGetValue(key, out RotatedTokens? found) || found is null) return false;

        _cache.Remove(key);
        tokens = found;
        return true;
    }

    // The raw token is never used as the key; a hash keeps secrets out of cache keys and any dump of
    // them, while still matching the value the browser presents.
    private static string Key(string refreshToken) =>
        KeyPrefix + Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(refreshToken)));
}
