using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Vitorize.Shared.Common;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Pages.Admin.Auth
{
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly ApiClient _apiClient;

        public LoginModel(ApiClient apiClient)
        {
            _apiClient = apiClient;
        }

        [BindProperty]
        public AdminLoginInputModel Input { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ReturnUrl { get; set; }

        public string? ErrorMessage { get; set; }

        public IActionResult OnGet(string? returnUrl = null)
        {
            ReturnUrl = returnUrl;

            if (User.Identity?.IsAuthenticated == true &&
                (User.IsInRole("Admin") || User.IsInRole("SuperAdmin")))
            {
                return RedirectToSafeLocalUrl(ReturnUrl);
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            ReturnUrl = string.IsNullOrWhiteSpace(ReturnUrl)
                ? returnUrl
                : ReturnUrl;

            if (!ModelState.IsValid)
                return Page();

            var request = new AdminLoginApiRequestModel
            {
                Mobile = Input.Mobile.Trim(),
                Password = Input.Password
            };

            var result = await _apiClient.PostAsync<AdminLoginResponseModel>(
                "auth/login",
                request);

            if (!result.IsSuccess || result.Data == null)
            {
                ErrorMessage = string.IsNullOrWhiteSpace(result.Message)
                    ? "ورود ناموفق بود. لطفاً شماره موبایل و رمز عبور را بررسی کنید."
                    : result.Message;

                return Page();
            }

            var accessToken = result.Data.GetAccessToken();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                ErrorMessage = "توکن ورود از API دریافت نشد.";
                return Page();
            }

            var roles = ExtractRoles(result.Data, accessToken);

            if (!IsAdminRole(roles))
            {
                await HttpContext.SignOutAsync(VitorizeAuthSchemes.AdminScheme);

                Response.Cookies.Delete("Vitorize.Admin.Auth");
                Response.Cookies.Delete("Vitorize.Admin.AccessToken");
                Response.Cookies.Delete("Vitorize.Admin.RefreshToken");

                ErrorMessage = "این حساب دسترسی ادمین ندارد.";
                return Page();
            }

            var expiresUtc = Input.RememberMe
                ? DateTimeOffset.UtcNow.AddDays(7)
                : DateTimeOffset.UtcNow.AddHours(8);

            var displayName = result.Data.GetDisplayName(Input.Mobile);
            var userId = result.Data.GetUserId();

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId),
                new Claim(ClaimTypes.Name, displayName),
                new Claim("access_token", accessToken)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var identity = new ClaimsIdentity(
                claims,
                VitorizeAuthSchemes.AdminScheme);

            var principal = new ClaimsPrincipal(identity);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = Input.RememberMe,
                ExpiresUtc = expiresUtc,
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                VitorizeAuthSchemes.AdminScheme,
                principal,
                authProperties);

            AppendAuthCookies(
                accessToken,
                result.Data.RefreshToken,
                expiresUtc);

            return RedirectToSafeLocalUrl(ReturnUrl);
        }

        private void AppendAuthCookies(
            string accessToken,
            string? refreshToken,
            DateTimeOffset expiresUtc)
        {
            var accessTokenCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = Request.IsHttps,
                SameSite = SameSiteMode.Lax,
                Expires = expiresUtc
            };

            Response.Cookies.Append(
                "Vitorize.Admin.AccessToken",
                accessToken,
                accessTokenCookieOptions);

            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                Response.Cookies.Append(
                    "Vitorize.Admin.RefreshToken",
                    refreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = Input.RememberMe
                            ? DateTimeOffset.UtcNow.AddDays(30)
                            : expiresUtc
                    });
            }
        }

        private IActionResult RedirectToSafeLocalUrl(string? returnUrl)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) &&
                Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToPage("/Admin/Index");
        }

        private static HashSet<string> ExtractRoles(
            AdminLoginResponseModel response,
            string accessToken)
        {
            var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            AddRoles(roles, response.Roles);
            AddRoles(roles, response.RoleNames);
            AddRole(roles, response.Role);

            if (response.IsAdmin)
                roles.Add("Admin");

            if (response.User != null)
            {
                AddRoles(roles, response.User.Roles);
                AddRoles(roles, response.User.RoleNames);
                AddRole(roles, response.User.Role);

                if (response.User.IsAdmin)
                    roles.Add("Admin");
            }

            AddRoles(roles, ExtractRolesFromJwt(accessToken));

            return roles;
        }

        private static bool IsAdminRole(IEnumerable<string> roles)
        {
            return roles.Any(role =>
                string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(role, "SuperAdmin", StringComparison.OrdinalIgnoreCase));
        }

        private static void AddRoles(HashSet<string> target, IEnumerable<string>? roles)
        {
            if (roles == null)
                return;

            foreach (var role in roles)
            {
                AddRole(target, role);
            }
        }

        private static void AddRole(HashSet<string> target, string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
                return;

            var trimmed = role.Trim();

            if (trimmed.StartsWith("[") && trimmed.EndsWith("]"))
            {
                try
                {
                    var values = JsonSerializer.Deserialize<List<string>>(trimmed);

                    if (values != null)
                    {
                        foreach (var value in values)
                        {
                            AddRole(target, value);
                        }
                    }

                    return;
                }
                catch
                {
                    // Invalid role json ignored.
                }
            }

            var parts = trimmed.Split(
                new[] { ',', ';' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                    target.Add(part);
            }
        }

        private static IEnumerable<string> ExtractRolesFromJwt(string token)
        {
            var roles = new List<string>();

            try
            {
                var parts = token.Split('.');

                if (parts.Length < 2)
                    return roles;

                var payloadJson = DecodeBase64Url(parts[1]);

                using var document = JsonDocument.Parse(payloadJson);

                foreach (var property in document.RootElement.EnumerateObject())
                {
                    var claimName = property.Name;

                    var isRoleClaim =
                        claimName.Equals("role", StringComparison.OrdinalIgnoreCase) ||
                        claimName.Equals("roles", StringComparison.OrdinalIgnoreCase) ||
                        claimName.EndsWith("/role", StringComparison.OrdinalIgnoreCase);

                    if (!isRoleClaim)
                        continue;

                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in property.Value.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                                roles.Add(item.GetString() ?? string.Empty);
                        }
                    }
                    else if (property.Value.ValueKind == JsonValueKind.String)
                    {
                        roles.Add(property.Value.GetString() ?? string.Empty);
                    }
                }

                return roles
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return roles;
            }
        }

        private static string DecodeBase64Url(string value)
        {
            var base64 = value
                .Replace('-', '+')
                .Replace('_', '/');

            switch (base64.Length % 4)
            {
                case 2:
                    base64 += "==";
                    break;

                case 3:
                    base64 += "=";
                    break;
            }

            var bytes = Convert.FromBase64String(base64);

            return Encoding.UTF8.GetString(bytes);
        }
    }
}