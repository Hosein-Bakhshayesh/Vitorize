using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public LoginModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public LoginRequestModel Input { get; set; } = new();

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet(string? returnUrl = null)
        {
            Input.ReturnUrl = returnUrl;

            if (User.Identity?.IsAuthenticated == true)
                return RedirectToPage("/Admin/Index");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PostAsync<AuthResponseModel>(
                "auth/login",
                new
                {
                    Input.Mobile,
                    Input.Password
                });

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            if (!AuthCookieWriter.HasAnyRole(result.Data.AccessToken, "Admin", "SuperAdmin"))
            {
                ErrorMessage = "این صفحه فقط مخصوص مدیران سایت است.";
                return Page();
            }

            await AuthCookieWriter.SignInAsync(
                HttpContext,
                result.Data,
                VitorizeAuthSchemes.AdminScheme,
                VitorizeAuthSchemes.AdminAccessTokenCookie,
                VitorizeAuthSchemes.AdminRefreshTokenCookie,
                DateTimeOffset.UtcNow.AddHours(8));

            if (!string.IsNullOrWhiteSpace(Input.ReturnUrl) && Url.IsLocalUrl(Input.ReturnUrl))
                return LocalRedirect(Input.ReturnUrl);

            return RedirectToPage("/Admin/Index");
        }
    }
}
