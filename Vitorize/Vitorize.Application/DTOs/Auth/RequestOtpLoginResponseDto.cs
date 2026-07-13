namespace Vitorize.Application.DTOs.Auth
{
    /// <summary>
    /// پاسخ درخواست کد ورود. پیام همیشه عمومی است و وجود/عدم‌وجود حساب را افشا نمی‌کند.
    /// </summary>
    public class RequestOtpLoginResponseDto
    {
        public string MaskedMobile { get; set; } = string.Empty;
        public int ExpirySeconds { get; set; }
        public int ResendCooldownSeconds { get; set; }
    }
}
