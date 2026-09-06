using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// درگاه سطح‌پایین ارائه‌دهنده پیامک. تنظیمات محرمانه فقط به Adapter زیرساختی پاس داده می‌شود
    /// و هرگز در API یا کلاینت برگردانده نمی‌شود.
    /// </summary>
    public interface ISmsSender
    {
        /// <summary>ارسال قالبی (Verify) برای پیام‌های تراکنشی/کد یکبار‌مصرف.</summary>
        Task<SmsSendResult> SendVerifyAsync(
            SmsOptions options,
            string mobile,
            int templateId,
            IReadOnlyList<SmsTemplateParameter> parameters,
            CancellationToken cancellationToken = default);

        /// <summary>ارسال متن ساده (Bulk) روی خط اختصاصی.</summary>
        Task<SmsSendResult> SendBulkAsync(
            SmsOptions options,
            string text,
            string mobile,
            CancellationToken cancellationToken = default);

        /// <summary>دریافت موجودی و خطوط حساب (برای بررسی سلامت در پنل ادمین).</summary>
        Task<SmsAccountStatus> GetAccountStatusAsync(
            SmsOptions options,
            CancellationToken cancellationToken = default);
    }
}
