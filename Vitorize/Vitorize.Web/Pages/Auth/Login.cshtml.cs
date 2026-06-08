using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Auth
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

            if (User.Identity?.IsAuthenticated == true && string.IsNullOrWhiteSpace(returnUrl))
                return RedirectToPage("/Index");

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

            if (!AuthCookieWriter.IsOnlyCustomer(result.Data.AccessToken))
            {
                ErrorMessage = "برای حساب‌های مدیریتی از صفحه ورود مدیران استفاده کنید.";
                return Page();
            }

            await AuthCookieWriter.SignInAsync(
                HttpContext,
                result.Data,
                VitorizeAuthSchemes.CustomerScheme,
                VitorizeAuthSchemes.CustomerAccessTokenCookie,
                VitorizeAuthSchemes.CustomerRefreshTokenCookie,
                DateTimeOffset.UtcNow.AddDays(7));

            if (!string.IsNullOrWhiteSpace(Input.ReturnUrl) && Url.IsLocalUrl(Input.ReturnUrl))
                return LocalRedirect(Input.ReturnUrl);

            return RedirectToPage("/Index");
        }
    }
}
