using Vitorize.Application.Common;

namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// عکس فوری تنظیمات پیامک که از جدول Settings خوانده و به‌صورت امن کش می‌شود.
    /// شامل مقادیر محرمانه است؛ فقط باید به سرویس‌های لایه Infrastructure تزریق شود
    /// و هرگز مستقیماً به کلاینت/پاسخ عمومی بازنگردد.
    /// </summary>
    public sealed class SmsOptions
    {
        public string Provider { get; init; } = SmsProviderNames.SmsIr;
        public string? ApiKey { get; init; }
        public long? DefaultLineNumber { get; init; }
        public string? SenderName { get; init; }

        // These values are populated before the transport is switched to Asanak.
        // They intentionally do not alter the currently registered SMS.ir sender.
        public string? AsanakUsername { get; init; }
        public string? AsanakPassword { get; init; }
        public string? AsanakSource { get; init; }

        public bool HasAsanakCredentials =>
            !string.IsNullOrWhiteSpace(AsanakUsername) &&
            !string.IsNullOrWhiteSpace(AsanakPassword);

        public bool CanSendAsanakText =>
            HasAsanakCredentials && !string.IsNullOrWhiteSpace(AsanakSource);

        /// <summary>نگاشت کلید منطقی قالب → شناسه قالب SMS.ir (اگر تنظیم شده باشد).</summary>
        public IReadOnlyDictionary<string, int> TemplateIds { get; init; }
            = new Dictionary<string, int>();

        public int MaxRetryCount { get; init; } = 3;
        public int RetryDelaySeconds { get; init; } = 30;

        public int OtpExpiryMinutes { get; init; } = 3;
        public int OtpResendCooldownSeconds { get; init; } = 90;
        public int OtpMaxAttempts { get; init; } = 5;
        public int DailyOtpLimitPerMobile { get; init; } = 10;
        public int DailySmsLimitPerMobile { get; init; } = 30;

        public bool LogSensitiveData { get; init; }
        public bool IsSmsIr => SmsProviderNames.IsSmsIr(Provider);
        public bool IsAsanak => SmsProviderNames.IsAsanak(Provider);

        /// <summary>آماده‌بودن پیامک متنی آزاد بر اساس Provider فعال.</summary>
        public bool CanSendText => IsSmsIr
            ? !string.IsNullOrWhiteSpace(ApiKey) && DefaultLineNumber is > 0
            : IsAsanak && CanSendAsanakText;

        public bool CanSendNotificationText => CanSendText;

        public const string TextSendingNotReadyMessage =
            "برای ارسال پیامک متنی سفارشی، اطلاعات اتصال و شماره مبدأ Provider فعال را در تنظیمات ← اعلان‌ها وارد کنید.";

        public int? GetTemplateId(string templateKey) =>
            TemplateIds.TryGetValue(templateKey, out var id) && id > 0 ? id : null;

        /// <summary>پیکربندی حداقلی لازم برای ارسال واقعی موجود است؟</summary>
        public bool IsOperational => IsSmsIr
            ? !string.IsNullOrWhiteSpace(ApiKey)
            : IsAsanak && HasAsanakCredentials;
    }
}
