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
        public const string ProviderName = "Asanak";

        public string? AsanakUsername { get; init; }
        public string? AsanakPassword { get; init; }
        public string? AsanakSource { get; init; }

        public bool HasAsanakCredentials =>
            !string.IsNullOrWhiteSpace(AsanakUsername) &&
            !string.IsNullOrWhiteSpace(AsanakPassword);

        public bool CanSendAsanakText =>
            HasAsanakCredentials && !string.IsNullOrWhiteSpace(AsanakSource);

        /// <summary>
        /// فقط برای خواندن payloadهای قدیمی Outbox نگه داشته شده است؛ ارسال جدید هیچ قالبی ندارد.
        /// </summary>
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
        public bool CanSendText => CanSendAsanakText;

        public bool CanSendNotificationText => CanSendText;

        public const string TextSendingNotReadyMessage =
            "برای ارسال پیامک متنی سفارشی، نام کاربری، رمز وب‌سرویس و شماره مبدأ آسانک را در تنظیمات ← اعلان‌ها وارد کنید.";

        public int? GetTemplateId(string templateKey) =>
            TemplateIds.TryGetValue(templateKey, out var id) && id > 0 ? id : null;

        /// <summary>پیکربندی حداقلی لازم برای ارسال واقعی موجود است؟</summary>
        public bool IsOperational => HasAsanakCredentials;
    }
}
