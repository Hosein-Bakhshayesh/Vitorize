using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Auth
{
    public class LoginRequestModel
    {
        [Required(ErrorMessage = "شماره موبایل الزامی است.")]
        [Display(Name = "شماره موبایل")]
        public string Mobile { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class RegisterRequestModel
    {
        [Required(ErrorMessage = "نام و نام خانوادگی الزامی است.")]
        [Display(Name = "نام و نام خانوادگی")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "شماره موبایل الزامی است.")]
        [Display(Name = "شماره موبایل")]
        public string Mobile { get; set; } = string.Empty;

        [EmailAddress(ErrorMessage = "ایمیل معتبر نیست.")]
        [Display(Name = "ایمیل")]
        public string? Email { get; set; }

        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        [MinLength(6, ErrorMessage = "رمز عبور حداقل باید ۶ کاراکتر باشد.")]
        [Display(Name = "رمز عبور")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponseModel
    {
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string AccessToken { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime AccessTokenExpiresAt { get; set; }
        public DateTime RefreshTokenExpiresAt { get; set; }
    }

    public class CurrentUserModel
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string? Email { get; set; }
        public byte Status { get; set; }
        public byte VerificationStatus { get; set; }
        public bool IsMobileConfirmed { get; set; }
        public bool IsEmailConfirmed { get; set; }
    }
}
