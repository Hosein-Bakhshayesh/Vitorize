using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services.Sms
{
    /// <summary>
    /// خواننده امن و کش‌شده تنظیمات پیامک از جدول Settings.
    /// Singleton است و برای هر بار بارگذاری یک scope موقت می‌سازد.
    /// TTL کوتاه (پیش‌فرض ۶۰ ثانیه) دارد و با تغییر تنظیمات توسط ادمین باطل می‌شود.
    /// </summary>
    public sealed class SmsSettingsProvider : ISmsSettingsProvider
    {
        private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _lock = new(1, 1);

        private volatile SmsOptions? _cached;
        private DateTime _expiresAtUtc = DateTime.MinValue;

        private static readonly IReadOnlyDictionary<string, string> TemplateSettingToKey =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                [SmsSettingKeys.OtpTemplateId] = SmsTemplateKeys.GenericOtp,
                [SmsSettingKeys.LoginOtpTemplateId] = SmsTemplateKeys.LoginOtp,
                [SmsSettingKeys.RegisterOtpTemplateId] = SmsTemplateKeys.RegisterOtp,
                [SmsSettingKeys.ForgotPasswordTemplateId] = SmsTemplateKeys.ForgotPassword,
                [SmsSettingKeys.OrderPaidTemplateId] = SmsTemplateKeys.OrderPaid,
                [SmsSettingKeys.OrderCompletedTemplateId] = SmsTemplateKeys.OrderCompleted,
                [SmsSettingKeys.GiftCodeDeliveredTemplateId] = SmsTemplateKeys.GiftCodeDelivered,
                [SmsSettingKeys.TicketReplyTemplateId] = SmsTemplateKeys.TicketReply,
                [SmsSettingKeys.VerificationApprovedTemplateId] = SmsTemplateKeys.VerificationApproved,
                [SmsSettingKeys.VerificationRejectedTemplateId] = SmsTemplateKeys.VerificationRejected,
                [SmsSettingKeys.OrderStatusChangedTemplateId] = SmsTemplateKeys.OrderStatusChanged,
                [SmsSettingKeys.WalletTopUpSuccessTemplateId] = SmsTemplateKeys.WalletTopUpSuccess,
            };

        public SmsSettingsProvider(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public async Task<SmsOptions> GetAsync(CancellationToken cancellationToken = default)
        {
            var snapshot = _cached;
            if (snapshot is not null && DateTime.UtcNow < _expiresAtUtc)
                return snapshot;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                if (_cached is not null && DateTime.UtcNow < _expiresAtUtc)
                    return _cached;

                var options = await LoadAsync(cancellationToken);
                _cached = options;
                _expiresAtUtc = DateTime.UtcNow.Add(Ttl);
                return options;
            }
            finally
            {
                _lock.Release();
            }
        }

        public void Invalidate()
        {
            _cached = null;
            _expiresAtUtc = DateTime.MinValue;
        }

        private async Task<SmsOptions> LoadAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();

            var rows = await db.Settings
                .AsNoTracking()
                .Where(x => x.Key.StartsWith("Sms."))
                .Select(x => new { x.Key, x.Value })
                .ToListAsync(cancellationToken);

            var map = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows)
                map[row.Key] = row.Value;

            var templateIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in TemplateSettingToKey)
            {
                if (map.TryGetValue(pair.Key, out var raw) &&
                    int.TryParse(raw, out var id) && id > 0)
                {
                    templateIds[pair.Value] = id;
                }
            }

            return new SmsOptions
            {
                IsEnabled = GetBool(map, SmsSettingKeys.IsEnabled, false),
                Provider = GetString(map, SmsSettingKeys.Provider) ?? "SMS.ir",
                ApiKey = GetString(map, SmsSettingKeys.ApiKey),
                DefaultLineNumber = GetLong(map, SmsSettingKeys.DefaultLineNumber),
                SenderName = GetString(map, SmsSettingKeys.SenderName),
                TemplateIds = templateIds,
                MaxRetryCount = GetInt(map, SmsSettingKeys.MaxRetryCount, 3),
                RetryDelaySeconds = GetInt(map, SmsSettingKeys.RetryDelaySeconds, 30),
                OtpExpiryMinutes = GetInt(map, SmsSettingKeys.OtpExpiryMinutes, 3),
                OtpResendCooldownSeconds = GetInt(map, SmsSettingKeys.OtpResendCooldownSeconds, 90),
                OtpMaxAttempts = GetInt(map, SmsSettingKeys.OtpMaxAttempts, 5),
                DailyOtpLimitPerMobile = GetInt(map, SmsSettingKeys.DailyOtpLimitPerMobile, 10),
                DailySmsLimitPerMobile = GetInt(map, SmsSettingKeys.DailySmsLimitPerMobile, 30),
                LogSensitiveData = GetBool(map, SmsSettingKeys.LogSensitiveData, false),
                UseOutbox = GetBool(map, SmsSettingKeys.UseOutbox, true)
            };
        }

        private static string? GetString(IReadOnlyDictionary<string, string?> map, string key) =>
            map.TryGetValue(key, out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : null;

        private static bool GetBool(IReadOnlyDictionary<string, string?> map, string key, bool fallback) =>
            map.TryGetValue(key, out var v) && bool.TryParse(v, out var b) ? b : fallback;

        private static int GetInt(IReadOnlyDictionary<string, string?> map, string key, int fallback) =>
            map.TryGetValue(key, out var v) && int.TryParse(v, out var i) ? i : fallback;

        private static long? GetLong(IReadOnlyDictionary<string, string?> map, string key) =>
            map.TryGetValue(key, out var v) && long.TryParse(v, out var l) ? l : (long?)null;
    }
}
