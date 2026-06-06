using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Web.Models.Auth;
using Vitorize.Web.Services;

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

        public IActionResult OnGet()
        {
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
                Input);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = result.Message;
                return Page();
            }

            if (string.IsNullOrWhiteSpace(result.Data.AccessToken))
            {
                ErrorMessage = "AccessToken از API دریافت نشد.";
                return Page();
            }

            var user = result.Data.User;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user?.Id.ToString() ?? string.Empty),
                new Claim(ClaimTypes.Name, user?.FullName ?? "مدیر سیستم"),
                new Claim("mobile", user?.Mobile ?? Input.Mobile),
                new Claim("access_token", result.Data.AccessToken),
                new Claim("refresh_token", result.Data.RefreshToken ?? string.Empty)
            };

            if (user?.Roles != null)
            {
                foreach (var role in user.Roles)
                    claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(
                claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
                });

            Response.Cookies.Append(
                "Vitorize.AccessToken",
                result.Data.AccessToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = DateTimeOffset.UtcNow.AddHours(8),
                    Path = "/"
                });

            if (!string.IsNullOrWhiteSpace(result.Data.RefreshToken))
            {
                Response.Cookies.Append(
                    "Vitorize.RefreshToken",
                    result.Data.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        Path = "/"
                    });
            }

            return RedirectToPage("/Admin/Index");
        }
    }
}