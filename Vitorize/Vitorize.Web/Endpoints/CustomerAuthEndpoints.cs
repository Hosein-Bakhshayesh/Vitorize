using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints
{
    /// <summary>
    /// ورود/ثبت‌نام/خروج مشتری به‌صورت endpoint سرور تا کوکی احراز هویت مشتری
    /// (مجزا از ادمین) پیش از شروع رندر نوشته شود.
    /// </summary>
    public static class CustomerAuthEndpoints
    {
        public static void MapCustomerAuthEndpoints(this WebApplication app)
        {
            app.MapPost("/auth/customer/login", LoginAsync).DisableAntiforgery();
            app.MapPost("/auth/customer/login/otp/complete", OtpCompleteAsync).DisableAntiforgery();
            app.MapPost("/auth/customer/register", RegisterAsync).DisableAntiforgery();
            app.MapPost("/auth/customer/logout", LogoutAsync).DisableAntiforgery();
        }

        /// <summary>
        /// مرحله نهایی ورود با کد یکبار‌مصرف: توکن‌های صادرشده توسط API (پس از تایید موفق کد در
        /// مدار تعاملی) را می‌گیرد و کوکی احراز هویت مشتری را می‌نویسد. هیچ کد یا رمزی اینجا نیست.
        /// </summary>
        private static async Task OtpCompleteAsync(HttpContext http)
        {
            var form = await http.Request.ReadFormAsync();
            var accessToken = form["accessToken"].ToString();
            var refreshToken = form["refreshToken"].ToString();
            var mobile = form["mobile"].ToString().Trim();
            var fullName = form["fullName"].ToString().Trim();
            var userId = form["userId"].ToString().Trim();
            var returnUrl = form["returnUrl"].ToString();

            if (string.IsNullOrWhiteSpace(accessToken))
            {
                http.Response.Redirect(FailUrl("/login", "ورود ناموفق بود. لطفاً دوباره تلاش کنید.", returnUrl) + "&otp=1");
                return;
            }

            var data = new AdminLoginResponseModel
            {
                UserId = Guid.TryParse(userId, out var uid) ? uid : Guid.Empty,
                FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                Mobile = string.IsNullOrWhiteSpace(mobile) ? null : mobile,
                AccessToken = accessToken,
                RefreshToken = string.IsNullOrWhiteSpace(refreshToken) ? null : refreshToken
            };

            await SignInCustomerAsync(http, data, mobile);
            http.Response.Redirect(SafeReturn(returnUrl));
        }

        private static async Task LoginAsync(HttpContext http, ApiClient apiClient)
        {
            var form = await http.Request.ReadFormAsync();
            var mobile = form["mobile"].ToString().Trim();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            if (string.IsNullOrWhiteSpace(mobile) || string.IsNullOrWhiteSpace(password))
            {
                http.Response.Redirect(FailUrl("/login", "شماره موبایل و رمز عبور الزامی است.", returnUrl));
                return;
            }

            var result = await apiClient.PostAsync<AdminLoginResponseModel>(
                "auth/login",
                new { Mobile = mobile, Password = password });

            if (!result.IsSuccess || result.Data is null)
            {
                http.Response.Redirect(FailUrl("/login",
                    string.IsNullOrWhiteSpace(result.Message)
                        ? "ورود ناموفق بود. شماره موبایل یا رمز عبور نادرست است."
                        : result.Message,
                    returnUrl));
                return;
            }

            await SignInCustomerAsync(http, result.Data, mobile);
            http.Response.Redirect(SafeReturn(returnUrl));
        }

        private static async Task RegisterAsync(HttpContext http, ApiClient apiClient)
        {
            var form = await http.Request.ReadFormAsync();
            var fullName = form["fullName"].ToString().Trim();
            var mobile = form["mobile"].ToString().Trim();
            var email = form["email"].ToString().Trim();
            var password = form["password"].ToString();
            var returnUrl = form["returnUrl"].ToString();

            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(mobile) ||
                string.IsNullOrWhiteSpace(password))
            {
                http.Response.Redirect(FailUrl("/register", "تکمیل نام، موبایل و رمز عبور الزامی است.", returnUrl));
                return;
            }

            var result = await apiClient.PostAsync<AdminLoginResponseModel>(
                "auth/register",
                new
                {
                    FullName = fullName,
                    Mobile = mobile,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email,
                    Password = password
                });

            if (!result.IsSuccess || result.Data is null)
            {
                http.Response.Redirect(FailUrl("/register",
                    string.IsNullOrWhiteSpace(result.Message)
                        ? "ثبت‌نام ناموفق بود. لطفاً دوباره تلاش کنید."
                        : result.Message,
                    returnUrl));
                return;
            }

            await SignInCustomerAsync(http, result.Data, mobile);
            http.Response.Redirect(SafeReturn(returnUrl));
        }

        private static async Task LogoutAsync(HttpContext http)
        {
            await http.SignOutAsync(VitorizeAuthSchemes.CustomerScheme);
            http.Response.Cookies.Delete(VitorizeAuthSchemes.CustomerAccessTokenCookie);
            http.Response.Cookies.Delete(VitorizeAuthSchemes.CustomerRefreshTokenCookie);
            http.Response.Redirect("/");
        }

        private static async Task SignInCustomerAsync(
            HttpContext http,
            AdminLoginResponseModel data,
            string mobile)
        {
            var accessToken = data.GetAccessToken();
            var expiresUtc = DateTimeOffset.UtcNow.AddDays(14);

            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, data.GetUserId()),
                new(ClaimTypes.Name, data.GetDisplayName(mobile)),
                new("mobile", data.Mobile ?? mobile),
                new("access_token", accessToken),
                new("refresh_token", data.RefreshToken ?? string.Empty)
            };

            foreach (var role in JwtHelper.ExtractRoles(accessToken))
                claims.Add(new Claim(ClaimTypes.Role, role));

            var identity = new ClaimsIdentity(claims, VitorizeAuthSchemes.CustomerScheme);
            var principal = new ClaimsPrincipal(identity);

            await http.SignInAsync(
                VitorizeAuthSchemes.CustomerScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = expiresUtc,
                    AllowRefresh = true
                });

            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = AuthCookiePolicy.IsSecure(http),
                SameSite = SameSiteMode.Lax,
                Expires = expiresUtc,
                Path = "/"
            };

            http.Response.Cookies.Append(
                VitorizeAuthSchemes.CustomerAccessTokenCookie, accessToken, cookieOptions);

            if (!string.IsNullOrWhiteSpace(data.RefreshToken))
            {
                http.Response.Cookies.Append(
                    VitorizeAuthSchemes.CustomerRefreshTokenCookie,
                    data.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = AuthCookiePolicy.IsSecure(http),
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        Path = "/"
                    });
            }
        }

        private static string FailUrl(string page, string message, string returnUrl)
        {
            var url = $"{page}?error={Uri.EscapeDataString(message)}";
            if (!string.IsNullOrWhiteSpace(returnUrl))
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            return url;
        }

        private static string SafeReturn(string? returnUrl) =>
            SafeRedirect.LocalOrDefault(returnUrl, "/customer/dashboard");
    }
}
