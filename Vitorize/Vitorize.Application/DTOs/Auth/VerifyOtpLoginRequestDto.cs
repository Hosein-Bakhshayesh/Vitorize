namespace Vitorize.Application.DTOs.Auth
{
    public class VerifyOtpLoginRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        /// <summary>اختیاری؛ برای هم‌ترازی با جریان احراز هویت موجود.</summary>
        public string? DeviceId { get; set; }
    }
}
