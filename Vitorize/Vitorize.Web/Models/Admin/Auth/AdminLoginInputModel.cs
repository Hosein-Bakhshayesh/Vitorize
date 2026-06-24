using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Auth
{
    public class AdminLoginInputModel
    {
        [Display(Name = "شماره موبایل ادمین")]
        [Required(ErrorMessage = "شماره موبایل ادمین الزامی است.")]
        public string Mobile { get; set; } = string.Empty;

        [Display(Name = "رمز عبور")]
        [Required(ErrorMessage = "رمز عبور الزامی است.")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool RememberMe { get; set; }
    }
}