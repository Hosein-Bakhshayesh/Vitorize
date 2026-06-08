using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Auth
{
    public class LogoutModel : PageModel
    {
        public async Task<IActionResult> OnGetAsync()
        {
            await LogoutAsync();
            return RedirectToPage("/Index");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            await LogoutAsync();
            return RedirectToPage("/Index");
        }

        private async Task LogoutAsync()
        {
            await AuthCookieWriter.SignOutAsync(
                HttpContext,
                VitorizeAuthSchemes.CustomerScheme,
                VitorizeAuthSchemes.CustomerAccessTokenCookie,
                VitorizeAuthSchemes.CustomerRefreshTokenCookie);
        }
    }
}
