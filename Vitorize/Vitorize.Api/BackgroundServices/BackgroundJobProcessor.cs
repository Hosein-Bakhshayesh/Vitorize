using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.BackgroundServices
{
    public class BackgroundJobProcessor : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BackgroundJobProcessor> _logger;
        private readonly IWorkerHeartbeatRegistry _heartbeatRegistry;

        public BackgroundJobProcessor(
            IServiceProvider serviceProvider,
            ILogger<BackgroundJobProcessor> logger,
            IWorkerHeartbeatRegistry heartbeatRegistry)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _heartbeatRegistry = heartbeatRegistry;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "Maintenance worker started. EventType={EventType} WorkerName={WorkerName}",
                OperationalEventNames.WorkerStarted, nameof(BackgroundJobProcessor));

            while (!stoppingToken.IsCancellationRequested)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();

                    await scope.ServiceProvider.GetRequiredService<IGiftCodeReservationService>()
                        .ReleaseExpiredReservationsAsync();
                    var reconciliationCount = await scope.ServiceProvider.GetRequiredService<IPaymentService>()
                        .ReconcilePendingZarinpalPaymentsAsync();
                    var processed = reconciliationCount;
                    processed += await CleanupOtp(db, stoppingToken);
                    processed += await CleanupRefreshTokens(db, stoppingToken);
                    processed += await CleanupIdempotency(db, stoppingToken);
                    processed += await ProtectLegacySensitiveData(db,
                        scope.ServiceProvider.GetRequiredService<IEncryptionService>(), stoppingToken);
                    processed += await CleanupOperationalLogs(db, stoppingToken);

                    stopwatch.Stop();
                    _heartbeatRegistry.Record(nameof(BackgroundJobProcessor), processed, stopwatch.Elapsed, "Succeeded");
                    if (processed > 0)
                    {
                        _logger.LogInformation(
                            "Maintenance worker iteration completed. EventType={EventType} WorkerName={WorkerName} BatchCount={BatchCount} ReconciledPayments={ReconciledPayments} DurationMs={DurationMs}",
                            OperationalEventNames.WorkerIterationCompleted, nameof(BackgroundJobProcessor), processed, reconciliationCount, stopwatch.ElapsedMilliseconds);
                    }
                    else
                    {
                        _logger.LogDebug(
                            "Maintenance worker idle. EventType={EventType} WorkerName={WorkerName} DurationMs={DurationMs}",
                            OperationalEventNames.WorkerIterationCompleted, nameof(BackgroundJobProcessor), stopwatch.ElapsedMilliseconds);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _heartbeatRegistry.Record(nameof(BackgroundJobProcessor), 0, stopwatch.Elapsed, "Failed");
                    _logger.LogError(
                        "Maintenance worker iteration failed. EventType={EventType} WorkerName={WorkerName} ExceptionType={ExceptionType} DurationMs={DurationMs}",
                        OperationalEventNames.WorkerIterationFailed, nameof(BackgroundJobProcessor), ex.GetType().Name, stopwatch.ElapsedMilliseconds);
                }

                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            _logger.LogInformation(
                "Maintenance worker stopped. EventType={EventType} WorkerName={WorkerName}",
                OperationalEventNames.WorkerStopped, nameof(BackgroundJobProcessor));
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

        private async Task<int> CleanupOtp(VitorizeDbContext db, CancellationToken ct)
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
            return old.Count;
        }

        private async Task<int> CleanupRefreshTokens(VitorizeDbContext db, CancellationToken ct)
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
            return tokens.Count;
        }

        private async Task<int> CleanupIdempotency(VitorizeDbContext db, CancellationToken ct)
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
            return keys.Count;
        }

        private static async Task<int> CleanupOperationalLogs(VitorizeDbContext db, CancellationToken ct)
        {
            var auditCutoff = DateTime.UtcNow.AddDays(-365);
            var securityCutoff = DateTime.UtcNow.AddDays(-730);
            var audits = await db.AuditLogs.Where(x => x.CreatedAt < auditCutoff).Take(500).ToListAsync(ct);
            var security = await db.SecurityLogs.Where(x => x.CreatedAt < securityCutoff).Take(500).ToListAsync(ct);
            if (audits.Count > 0) db.AuditLogs.RemoveRange(audits);
            if (security.Count > 0) db.SecurityLogs.RemoveRange(security);
            if (audits.Count > 0 || security.Count > 0) await db.SaveChangesAsync(ct);
            return audits.Count + security.Count;
        }

        private static async Task<int> ProtectLegacySensitiveData(
            VitorizeDbContext db,
            IEncryptionService encryption,
            CancellationToken ct)
        {
            var deliveries = await db.OrderItemDeliveries
                .Where(x => x.EncryptionVersion == null && x.DeliveredContent != null)
                .Take(100).ToListAsync(ct);
            foreach (var row in deliveries)
            {
                var plain = row.DeliveredContent!;
                row.DeliveredContent = encryption.Encrypt(plain);
                row.ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plain)));
                row.EncryptionVersion = 2;
            }

            var profiles = await db.UserVerificationProfiles
                .Where(x => x.EncryptedPayload == null).Take(100).ToListAsync(ct);
            foreach (var profile in profiles)
            {
                profile.EncryptedPayload = encryption.Encrypt(JsonSerializer.Serialize(new
                {
                    profile.FirstName, profile.LastName, profile.NationalCode, profile.BirthDate,
                    profile.BankCardNumber, profile.ShabaNumber, profile.Address, profile.PostalCode
                }));
                profile.EncryptionVersion = 2;
                profile.FirstName = "[protected]";
                profile.LastName = "[protected]";
                profile.NationalCode = "[protected]";
                profile.BirthDate = null;
                profile.BankCardNumber = null;
                profile.ShabaNumber = null;
                profile.Address = null;
                profile.PostalCode = null;
                var user = await db.Users.FirstOrDefaultAsync(x => x.Id == profile.UserId, ct);
                if (user is not null) user.NationalCode = null;
            }
            if (deliveries.Count > 0 || profiles.Count > 0) await db.SaveChangesAsync(ct);
            return deliveries.Count + profiles.Count;
        }
    }
}
