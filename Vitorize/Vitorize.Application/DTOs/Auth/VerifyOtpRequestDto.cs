namespace Vitorize.Application.DTOs.Auth
{
    public class VerifyOtpRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public byte Purpose { get; set; }
    }
}