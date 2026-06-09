namespace Vitorize.Application.DTOs.Admin.Users
{
    public class AdminUserFilterDto
    {
        public string? Search { get; set; }

        public byte? Status { get; set; }

        public byte? VerificationStatus { get; set; }

        public string? Role { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}