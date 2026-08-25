using Microsoft.Extensions.Caching.Memory;
using Vitorize.Application.DTOs.Auth;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Services
{
    /// <summary>
    /// In-process implementation of <see cref="IRefreshTokenRotationCache"/>.
    ///
    /// Entries expire absolutely, never on a sliding window: a replayable rotation result must not be
    /// kept alive by being replayed. Registered as a singleton so the window is shared across every
    /// request in the process, which is the whole point.
    /// </summary>
    public sealed class RefreshTokenRotationCache : IRefreshTokenRotationCache
    {
        private const string KeyPrefix = "refresh-grace:";
        private readonly IMemoryCache _cache;

        public RefreshTokenRotationCache(IMemoryCache cache) => _cache = cache;

        public bool TryGet(string spentTokenHash, out AuthResponseDto? result)
        {
            if (string.IsNullOrWhiteSpace(spentTokenHash))
            {
                result = null;
                return false;
            }

            return _cache.TryGetValue(KeyPrefix + spentTokenHash, out result) && result is not null;
        }

        public void Remember(string spentTokenHash, AuthResponseDto result)
        {
            if (string.IsNullOrWhiteSpace(spentTokenHash) || result is null) return;

            _cache.Set(
                KeyPrefix + spentTokenHash,
                result,
                new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = IRefreshTokenRotationCache.GraceWindow });
        }

        public void Forget(string spentTokenHash)
        {
            if (!string.IsNullOrWhiteSpace(spentTokenHash)) _cache.Remove(KeyPrefix + spentTokenHash);
        }
    }
}
