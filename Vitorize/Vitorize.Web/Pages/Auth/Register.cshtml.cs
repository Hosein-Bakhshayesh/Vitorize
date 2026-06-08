using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Auth
{
    public class RegisterModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public RegisterModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public RegisterRequestModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var result = await _apiClient.PostAsync<AuthResponseModel>(
                "auth/register",
                Input);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            await AuthCookieWriter.SignInAsync(
                HttpContext,
                result.Data,
                VitorizeAuthSchemes.CustomerScheme,
                VitorizeAuthSchemes.CustomerAccessTokenCookie,
                VitorizeAuthSchemes.CustomerRefreshTokenCookie,
                DateTimeOffset.UtcNow.AddDays(7));

            if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                return LocalRedirect(ReturnUrl);

            return RedirectToPage("/Index");
        }
    }
}
