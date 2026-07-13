using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// درگاه سطح‌پایین ارائه‌دهنده پیامک (Adapter روی SDK رسمی SMS.ir).
    /// کلید API به‌صورت صریح پاس داده می‌شود تا پیاده‌سازی بتواند singleton و
    /// بازاستفاده‌کننده از HttpClient باشد. تنها پیاده‌سازی مجاز، آداپتور SMS.ir است.
    /// </summary>
    public interface ISmsSender
    {
        /// <summary>ارسال قالبی (Verify) برای پیام‌های تراکنشی/کد یکبار‌مصرف.</summary>
        Task<SmsSendResult> SendVerifyAsync(
            string apiKey,
            string mobile,
            int templateId,
            IReadOnlyList<SmsTemplateParameter> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>ارسال متن ساده (Bulk) روی خط اختصاصی.</summary>
        Task<SmsSendResult> SendBulkAsync(
            string apiKey,
            long lineNumber,
            string text,
            string mobile,
            CancellationToken cancellationToken = default);

        /// <summary>دریافت موجودی و خطوط حساب (برای بررسی سلامت در پنل ادمین).</summary>
        Task<SmsAccountStatus> GetAccountStatusAsync(
            string apiKey,
            CancellationToken cancellationToken = default);
    }
}
