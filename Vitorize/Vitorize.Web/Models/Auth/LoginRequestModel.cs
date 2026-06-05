using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Auth
{
    public class LoginRequestModel
    {
        [Required(ErrorMessage = "شماره موبایل الزامی است.")]
        public string Mobile { get; set; } = string.Empty;

        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        public string Password { get; set; } = string.Empty;
    }
}