namespace Vitorize.Application.DTOs.Auth
{
    public class UpdateProfileRequestDto
    {
        public string FullName { get; set; } = string.Empty;

        public string? Email { get; set; }
    }
}
