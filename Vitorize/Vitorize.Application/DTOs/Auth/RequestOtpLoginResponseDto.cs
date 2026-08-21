namespace Vitorize.Application.DTOs.Auth
{
    /// <summary>
    /// پاسخ درخواست کد ورود.
    ///
    /// <see cref="Outcome"/> عمداً مشخص می‌کند که کد ارسال شده است یا شماره ثبت‌نام نشده؛ در غیر این
    /// صورت رابط کاربری کاربرِ ثبت‌نام‌نشده را به صفحه‌ی وارد کردن کدی می‌بَرد که هرگز ارسال نشده.
    /// بیش از همین یک واقعیت چیزی افشا نمی‌شود: نه شناسه، نه ایمیل و نه وضعیت حساب.
    /// </summary>
    public class RequestOtpLoginResponseDto
    {
        public string MaskedMobile { get; set; } = string.Empty;
        public int ExpirySeconds { get; set; }
        public int ResendCooldownSeconds { get; set; }

        /// <summary>یکی از مقادیر <c>AuthOutcomeCodes</c>.</summary>
        public string Outcome { get; set; } = Vitorize.Application.Common.AuthOutcomeCodes.OtpSent;
    }
}
