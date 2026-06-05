namespace Vitorize.Application.DTOs.Auth
{
    public class ResetPasswordRequestDto
    {
        public string Mobile { get; set; } = string.Empty;

        public string Code { get; set; } = string.Empty;

        public string NewPassword { get; set; } = string.Empty;

        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}