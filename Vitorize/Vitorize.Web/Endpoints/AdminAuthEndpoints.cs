using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints
{
    /// <summary>
    /// نقاط ورود/خروج ادمین به‌صورت endpoint سرور.
    /// از فرم استاندارد HTML پست می‌شوند تا کوکی احراز هویت
    /// پیش از شروع رندر نوشته شود.
    /// </summary>
    public static class AdminAuthEndpoints
    {
        public static void MapAdminAuthEndpoints(this WebApplication app)
        {
            app.MapPost("/admin/auth/login", LoginAsync).DisableAntiforgery();
            app.MapPost("/admin/auth/logout", LogoutAsync).DisableAntiforgery();
        }

        private static async Task LoginAsync(HttpContext http, ApiClient apiClient)
        {
            var form = await http.Request.ReadFormAsync();

            var mobile = form["mobile"].ToString().Trim();
            var password = form["password"].ToString();
            var rememberValue = form["rememberMe"].ToString();
            var rememberMe =
                rememberValue.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                rememberValue.Equals("on", StringComparison.OrdinalIgnoreCase);
            var returnUrl = form["returnUrl"].ToString();

            string FailUrl(string message)
            {
                var url = $"/admin/login?error={Uri.EscapeDataString(message)}";

                if (!string.IsNullOrWhiteSpace(returnUrl))
                    url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";

                return url;
            }

            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(password))
            {
                http.Response.Redirect(FailUrl("شماره موبایل و رمز عبور الزامی است."));
                return;
            }

            var result = await apiClient.PostAsync<AdminLoginResponseModel>(
                "auth/login",
                new { Mobile = mobile, Password = password });

            if (!result.IsSuccess || result.Data == null)
            {
                var message = string.IsNullOrWhiteSpace(result.Message)
                    ? "ورود ناموفق بود. لطفاً شماره موبایل و رمز عبور را بررسی کنید."
                    : result.Message;

                http.Response.Redirect(FailUrl(message));
                return;
            }

            var accessToken = result.Data.GetAccessToken();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                http.Response.Redirect(FailUrl("توکن ورود از سرور دریافت نشد."));
                return;
            }

            var roles = JwtHelper.ExtractRoles(accessToken);

            if (!JwtHelper.IsAdmin(roles))
            {
                http.Response.Redirect(FailUrl("این حساب کاربری دسترسی به پنل مدیریت ندارد."));
                return;
            }

            var expiresUtc = rememberMe
                ? DateTimeOffset.UtcNow.AddDays(7)
                : DateTimeOffset.UtcNow.AddHours(8);

            var displayName = result.Data.GetDisplayName(mobile);
            var userId = result.Data.GetUserId();

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, userId),
                new(ClaimTypes.Name, displayName),
                new("mobile", result.Data.Mobile ?? mobile),
                new("access_token", accessToken),
                new("refresh_token", result.Data.RefreshToken ?? string.Empty)
            };

            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, VitorizeAuthSchemes.AdminScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(
                VitorizeAuthSchemes.AdminScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = rememberMe,
                    ExpiresUtc = expiresUtc,
                    AllowRefresh = true
                });

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = expiresUtc,
                Path = "/"
            };

            http.Response.Cookies.Append(
                VitorizeAuthSchemes.AdminAccessTokenCookie,
                accessToken,
                cookieOptions);

            if (!string.IsNullOrWhiteSpace(result.Data.RefreshToken))
            {
                http.Response.Cookies.Append(
                    VitorizeAuthSchemes.AdminRefreshTokenCookie,
                    result.Data.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = rememberMe
                            ? DateTimeOffset.UtcNow.AddDays(30)
                            : expiresUtc,
                        Path = "/"
                    });
            }

            var safeReturn =
                !string.IsNullOrWhiteSpace(returnUrl) &&
                returnUrl.StartsWith("/") &&
                !returnUrl.StartsWith("//")
                    ? returnUrl
                    : "/admin/dashboard";

            http.Response.Redirect(safeReturn);
        }

        private static async Task LogoutAsync(HttpContext http)
        {
            await http.SignOutAsync(VitorizeAuthSchemes.AdminScheme);

            http.Response.Cookies.Delete(VitorizeAuthSchemes.AdminAccessTokenCookie);
            http.Response.Cookies.Delete(VitorizeAuthSchemes.AdminRefreshTokenCookie);

            http.Response.Redirect("/admin/login");
        }
    }
}
