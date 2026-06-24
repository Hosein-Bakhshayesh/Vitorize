namespace Vitorize.Web.Models.Admin.Auth
{
    public class AdminLoginResponseModel
    {
        public Guid UserId { get; set; }

        public string? FullName { get; set; }

        public string? Mobile { get; set; }

        public string? Email { get; set; }

        public string? UserName { get; set; }

        public string? AccessToken { get; set; }

        public string? Token { get; set; }

        public string? JwtToken { get; set; }

        public string? RefreshToken { get; set; }

        public DateTime? AccessTokenExpiresAt { get; set; }

        public DateTime? RefreshTokenExpiresAt { get; set; }

        public string? Role { get; set; }

        public bool IsAdmin { get; set; }

        public List<string>? Roles { get; set; }

        public List<string>? RoleNames { get; set; }

        public AdminLoginUserModel? User { get; set; }

        public string GetAccessToken()
        {
            return AccessToken ??
                   Token ??
                   JwtToken ??
                   string.Empty;
        }

        public string GetDisplayName(string fallback)
        {
            if (!string.IsNullOrWhiteSpace(User?.FullName))
                return User.FullName;

            if (!string.IsNullOrWhiteSpace(User?.UserName))
                return User.UserName;

            if (!string.IsNullOrWhiteSpace(User?.Email))
                return User.Email;

            if (!string.IsNullOrWhiteSpace(User?.Mobile))
                return User.Mobile;

            if (!string.IsNullOrWhiteSpace(FullName))
                return FullName;

            if (!string.IsNullOrWhiteSpace(UserName))
                return UserName;

            if (!string.IsNullOrWhiteSpace(Email))
                return Email;

            if (!string.IsNullOrWhiteSpace(Mobile))
                return Mobile;

            return fallback;
        }

        public string GetUserId()
        {
            if (User?.Id != null && User.Id.Value != Guid.Empty)
                return User.Id.Value.ToString();

            if (UserId != Guid.Empty)
                return UserId.ToString();

            return Guid.Empty.ToString();
        }
    }
}