using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Account
{
    [IgnoreAntiforgeryToken]
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            await LogoutAsync();
            return RedirectToPage("/Account/Login");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LogoutAsync();
            return RedirectToPage("/Account/Login");
        }

        private async Task LogoutAsync()
        {
            await HttpContext.SignOutAsync(VitorizeAuthSchemes.AdminScheme);

            Response.Cookies.Delete(VitorizeAuthSchemes.AdminAccessTokenCookie, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(VitorizeAuthSchemes.AdminRefreshTokenCookie, new CookieOptions { Path = "/" });
            Response.Cookies.Delete("Vitorize.Admin.Auth", new CookieOptions { Path = "/" });

            Response.Cookies.Delete("Vitorize.AccessToken", new CookieOptions { Path = "/" });
            Response.Cookies.Delete("Vitorize.RefreshToken", new CookieOptions { Path = "/" });
        }
    }
}