using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    /// <summary>
    /// Reads <c>MaintenanceMode</c> once and holds it for a few seconds.
    ///
    /// Registered as a singleton because the answer is the same for every caller, so it resolves its
    /// own scope to reach the DbContext. A read failure is treated as "not in maintenance": the flag
    /// is a deliberate administrative action, and a transient database hiccup must not take the shop
    /// down on its own.
    /// </summary>
    public sealed class MaintenanceStateProvider : IMaintenanceStateProvider
    {
        /// <summary>Short enough that an unexpected miss self-corrects, long enough to stop per-request queries.</summary>
        private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(15);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _value;
        private DateTime _loadedAtUtc = DateTime.MinValue;

        public MaintenanceStateProvider(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;

        public void Invalidate() => _loadedAtUtc = DateTime.MinValue;

        public async Task<bool> IsMaintenanceModeAsync(CancellationToken cancellationToken = default)
        {
            if (DateTime.UtcNow - _loadedAtUtc < CacheTtl) return _value;

            await _gate.WaitAsync(cancellationToken);
            try
            {
                if (DateTime.UtcNow - _loadedAtUtc < CacheTtl) return _value;

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();

                var raw = await db.Settings.AsNoTracking()
                    .Where(x => x.Key == "MaintenanceMode")
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync(cancellationToken);

                _value = bool.TryParse(raw, out var parsed) && parsed;
                _loadedAtUtc = DateTime.UtcNow;
                return _value;
            }
            catch
            {
                // Fail open, deliberately: an unreachable settings row must not close the shop.
                return _value;
            }
            finally
            {
                _gate.Release();
            }
        }
    }
}
