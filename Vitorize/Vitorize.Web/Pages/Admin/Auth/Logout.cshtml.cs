using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Auth
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            await SignOutAdminAsync();

            return RedirectToPage("/Admin/Auth/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await SignOutAdminAsync();

            return RedirectToPage("/Admin/Auth/Login");
        }

        private async Task SignOutAdminAsync()
        {
            await HttpContext.SignOutAsync(VitorizeAuthSchemes.AdminScheme);

            Response.Cookies.Delete("Vitorize.Admin.Auth");
            Response.Cookies.Delete("Vitorize.Admin.AccessToken");
            Response.Cookies.Delete("Vitorize.Admin.RefreshToken");
        }
    }
}