using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Users
{
    public class AdminUserModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? NationalCode { get; set; }
        public string? AvatarPath { get; set; }
        public byte Status { get; set; }
        public byte VerificationStatus { get; set; }
        public bool IsMobileConfirmed { get; set; }
        public bool IsEmailConfirmed { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public decimal WalletBalance { get; set; }
        public int OrdersCount { get; set; }
    }

    public class AdminUserDetailModel : AdminUserModel
    {
        public List<string> RoleNames { get; set; } = new();
    }

    public class AdminUserFilterModel
    {
        public string? Search { get; set; }
        public byte? Status { get; set; }
        public byte? VerificationStatus { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class UpdateUserRoleModel
    {
        [Required] public string RoleName { get; set; } = string.Empty;
    }
}
