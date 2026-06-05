namespace Vitorize.Application.DTOs.Auth
{
    public class SendOtpRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        public byte Purpose { get; set; }
    }
}