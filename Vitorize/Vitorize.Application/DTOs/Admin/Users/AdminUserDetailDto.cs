namespace Vitorize.Application.DTOs.Admin.Users
{
    public class AdminUserDetailDto : AdminUserDto
    {
        public string? NationalCode { get; set; }

        public string? AvatarPath { get; set; }

        public bool IsEmailConfirmed { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? DeletedAt { get; set; }
    }
}