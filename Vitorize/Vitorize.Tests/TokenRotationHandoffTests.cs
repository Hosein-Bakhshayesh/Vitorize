using Microsoft.Extensions.Caching.Memory;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The handover that stops a rotation from stranding the browser.
///
/// When the rotated pair cannot be written to cookies — an interactive circuit whose response has
/// already started, with JavaScript interop unavailable — the API has nonetheless spent the old
/// refresh token. Without somewhere to park the replacement, the browser keeps presenting a revoked
/// token and the next page load ends the session, which is the "clear your cookies" report.
/// </summary>
public class TokenRotationHandoffTests
{
    private static TokenRotationHandoff Create() =>
        new(new MemoryCache(new MemoryCacheOptions()));

    [Fact]
    public void A_parked_pair_is_claimed_by_the_token_the_browser_still_holds()
    {
        var handoff = Create();
        handoff.Remember("old-refresh", "Vitorize.Customer", "new-access", "new-refresh");

        Assert.True(handoff.TryTake("old-refresh", out var tokens));
        Assert.Equal("Vitorize.Customer", tokens!.Scheme);
        Assert.Equal("new-access", tokens.AccessToken);
        Assert.Equal("new-refresh", tokens.RefreshToken);
    }

    [Fact]
    public void A_pair_is_claimed_exactly_once()
    {
        // One-shot, so two concurrent requests cannot both adopt and then both rewrite the cookie.
        var handoff = Create();
        handoff.Remember("old-refresh", "Vitorize.Customer", "a", "b");

        Assert.True(handoff.TryTake("old-refresh", out _));
        Assert.False(handoff.TryTake("old-refresh", out var second));
        Assert.Null(second);
    }

    [Fact]
    public void Only_the_matching_token_can_claim_a_pair()
    {
        // Keyed by the presented refresh token, so one session can never adopt another's rotation.
        var handoff = Create();
        handoff.Remember("session-one", "Vitorize.Customer", "a", "b");

        Assert.False(handoff.TryTake("session-two", out var tokens));
        Assert.Null(tokens);
    }

    [Theory]
    [InlineData("", "a", "b")]
    [InlineData("old", "", "b")]
    [InlineData("old", "a", "")]
    public void Incomplete_input_is_not_parked(string oldToken, string access, string refresh)
    {
        var handoff = Create();
        handoff.Remember(oldToken, "Vitorize.Customer", access, refresh);

        Assert.False(handoff.TryTake(string.IsNullOrEmpty(oldToken) ? "old" : oldToken, out _));
    }

    [Fact]
    public void Nothing_is_claimed_when_nothing_was_parked()
    {
        Assert.False(Create().TryTake("anything", out var tokens));
        Assert.Null(tokens);
    }

    [Fact]
    public void The_raw_refresh_token_is_never_used_as_a_cache_key()
    {
        // Secrets should not end up in cache keys, where a memory dump or a diagnostic listing would
        // expose them. Round-tripping still works, so the hashing is transparent to callers.
        var cache = new KeyRecordingCache();
        var handoff = new TokenRotationHandoff(cache);
        const string secret = "super-secret-refresh-token";

        handoff.Remember(secret, "Vitorize.Admin", "a", "b");

        Assert.NotEmpty(cache.Keys);
        Assert.DoesNotContain(cache.Keys, key => key.Contains(secret, StringComparison.Ordinal));
        Assert.True(handoff.TryTake(secret, out _));
    }

    /// <summary>A real cache that also remembers which keys it was asked to store.</summary>
    private sealed class KeyRecordingCache : IMemoryCache
    {
        private readonly MemoryCache _inner = new(new MemoryCacheOptions());
        public List<string> Keys { get; } = [];

        public ICacheEntry CreateEntry(object key)
        {
            Keys.Add(key.ToString() ?? string.Empty);
            return _inner.CreateEntry(key);
        }

        public void Remove(object key) => _inner.Remove(key);
        public bool TryGetValue(object key, out object? value) => _inner.TryGetValue(key, out value);
        public void Dispose() => _inner.Dispose();
    }
}
