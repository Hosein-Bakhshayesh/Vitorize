namespace Vitorize.Web.Models.Admin.Auth
{
    public class AdminLoginUserModel
    {
        public Guid? Id { get; set; }

        public string? FullName { get; set; }

        public string? UserName { get; set; }

        public string? Email { get; set; }

        public string? Mobile { get; set; }

        public string? Role { get; set; }

        public bool IsAdmin { get; set; }

        public List<string>? Roles { get; set; }

        public List<string>? RoleNames { get; set; }
    }
}