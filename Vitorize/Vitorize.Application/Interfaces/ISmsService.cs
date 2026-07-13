using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// انتزاع سطح‌بالای پیامک برای کل برنامه. تمام ارسال‌ها باید از این سرویس عبور کنند؛
    /// هیچ کنترلر/کامپوننت/سرویس تجاری نباید مستقیماً SDK را صدا بزند.
    /// </summary>
    public interface ISmsService
    {
        // ---- کمکی ----
        bool TryNormalizeMobile(string? input, out string normalized);
        Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default);

        // ---- ارسال عمومی ----
        Task<SmsSendResult> SendTemplateAsync(
            string mobile,
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters,
            CancellationToken cancellationToken = default);

        Task<SmsSendResult> SendTextAsync(
            string mobile,
            string text,
            CancellationToken cancellationToken = default);

        Task<SmsSendResult> SendOtpAsync(
            string mobile,
            string templateKey,
            string code,
            int expiryMinutes,
            CancellationToken cancellationToken = default);

        Task<SmsSendResult> SendCustomTextAsync(
            string mobile,
            string text,
            CancellationToken cancellationToken = default);

        // ---- کد یکبار‌مصرف احراز هویت (ارسال هم‌زمان) ----
        Task<SmsSendResult> SendLoginOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken cancellationToken = default);
        Task<SmsSendResult> SendRegisterOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken cancellationToken = default);
        Task<SmsSendResult> SendForgotPasswordOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken cancellationToken = default);

        // ---- ادمین ----
        Task<SmsAccountStatus> GetAccountStatusAsync(CancellationToken cancellationToken = default);
        Task<(bool IsValid, string Message)> ValidateConfigurationAsync(CancellationToken cancellationToken = default);
        Task<int?> GetTemplateIdAsync(string templateKey, CancellationToken cancellationToken = default);
    }
}
