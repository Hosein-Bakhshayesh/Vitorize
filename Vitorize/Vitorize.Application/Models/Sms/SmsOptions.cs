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
        public bool IsEnabled { get; init; }
        public string Provider { get; init; } = "SMS.ir";
        public string? ApiKey { get; init; }
        public long? DefaultLineNumber { get; init; }
        public string? SenderName { get; init; }

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
        public bool UseOutbox { get; init; } = true;

        public int? GetTemplateId(string templateKey) =>
            TemplateIds.TryGetValue(templateKey, out var id) && id > 0 ? id : null;

        /// <summary>پیکربندی حداقلی لازم برای ارسال واقعی موجود است؟</summary>
        public bool IsOperational =>
            IsEnabled && !string.IsNullOrWhiteSpace(ApiKey);
    }
}
