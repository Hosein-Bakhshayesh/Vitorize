using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Api.BackgroundServices
{
    public class BackgroundJobProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundJobProcessor> _logger;

        public BackgroundJobProcessor(
            IServiceProvider serviceProvider,
            ILogger<BackgroundJobProcessor> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Job Processor started");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();

                    await ExpireGiftCodes(db, stoppingToken);
                    await CleanupOtp(db, stoppingToken);
                    await CleanupRefreshTokens(db, stoppingToken);
                    await CleanupIdempotency(db, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Background job error");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }
        private async Task ExpireGiftCodes(VitorizeDbContext db, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var expired = await db.GiftCodeReservations
                .Include(x => x.GiftCode)
                .Where(x =>
                    x.Status == (byte)GiftCodeReservationStatus.Active &&
                    x.ExpiresAt < now)
                .ToListAsync(ct);

            foreach (var r in expired)
            {
                r.Status = (byte)GiftCodeReservationStatus.Released;

                if (r.GiftCode != null)
                {
                    r.GiftCode.Status = (byte)GiftCodeStatus.Available;
                    r.GiftCode.ReservationExpiresAt = null;
                }
            }

            if (expired.Any())
                await db.SaveChangesAsync(ct);
        }

        private async Task CleanupOtp(VitorizeDbContext db, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var old = await db.OtpCodes
                .Where(x => x.ExpiresAt < now || x.ConsumedAt != null)
                .Take(500)
                .ToListAsync(ct);

            if (old.Any())
            {
                db.OtpCodes.RemoveRange(old);
                await db.SaveChangesAsync(ct);
            }
        }

        private async Task CleanupRefreshTokens(VitorizeDbContext db, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var tokens = await db.UserRefreshTokens
                .Where(x => x.ExpiresAt < now)
                .Take(500)
                .ToListAsync(ct);

            if (tokens.Any())
            {
                db.UserRefreshTokens.RemoveRange(tokens);
                await db.SaveChangesAsync(ct);
            }
        }

        private async Task CleanupIdempotency(VitorizeDbContext db, CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            var keys = await db.IdempotencyKeys
                .Where(x => x.ExpiresAt < now)
                .Take(500)
                .ToListAsync(ct);

            if (keys.Any())
            {
                db.IdempotencyKeys.RemoveRange(keys);
                await db.SaveChangesAsync(ct);
            }
        }
    }
}