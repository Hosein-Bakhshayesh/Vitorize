using Vitorize.Shared.Enums;

namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// نتیجه ساخت‌یافته ارسال پیامک. هیچ اطلاعات محرمانه‌ای (کلید API، متن کامل کد) در آن قرار نمی‌گیرد.
    /// </summary>
    public sealed class SmsSendResult
    {
        public bool IsSuccess { get; init; }

        public SmsFailureReason FailureReason { get; init; } = SmsFailureReason.None;

        /// <summary>شناسه پیام ارائه‌دهنده در صورت موفقیت.</summary>
        public string? ProviderMessageId { get; init; }

        /// <summary>هزینه ارسال در صورت اعلام توسط ارائه‌دهنده.</summary>
        public decimal? Cost { get; init; }

        /// <summary>کد وضعیت خام ارائه‌دهنده (برای لاگ فنی، نه نمایش به کاربر).</summary>
        public int? ProviderStatus { get; init; }

        /// <summary>پیام فنی ارائه‌دهنده (فقط برای لاگ داخلی).</summary>
        public string? ProviderMessage { get; init; }

        /// <summary>پیام امن و فارسی برای نمایش به کاربر در صورت شکست.</summary>
        public string? UserMessage { get; init; }

        public static SmsSendResult Success(
            string? providerMessageId,
            decimal? cost = null,
            int? providerStatus = null,
            string? providerMessage = null) =>
            new()
            {
                IsSuccess = true,
                FailureReason = SmsFailureReason.None,
                ProviderMessageId = providerMessageId,
                Cost = cost,
                ProviderStatus = providerStatus,
                ProviderMessage = providerMessage
            };

        public static SmsSendResult Failure(
            SmsFailureReason reason,
            string? userMessage = null,
            int? providerStatus = null,
            string? providerMessage = null) =>
            new()
            {
                IsSuccess = false,
                FailureReason = reason,
                UserMessage = userMessage ?? DefaultUserMessage,
                ProviderStatus = providerStatus,
                ProviderMessage = providerMessage
            };

        public const string DefaultUserMessage =
            "ارسال پیامک با مشکل مواجه شد. لطفاً چند لحظه بعد دوباره تلاش کنید.";
    }

    /// <summary>نتیجه بررسی اعتبار/موجودی حساب پیامک (برای پنل ادمین).</summary>
    public sealed class SmsAccountStatus
    {
        public bool IsSuccess { get; init; }
        public decimal? Credit { get; init; }
        public IReadOnlyList<long>? Lines { get; init; }
        public string? UserMessage { get; init; }
    }
}
