using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Models.Admin.Auth;
using Vitorize.Web.Models.Store;
using Vitorize.Web.Services;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Cart;

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
            app.MapPost("/auth/customer/register/verify", VerifyRegistrationAsync).DisableAntiforgery();
            app.MapPost("/auth/customer/register/resend", ResendRegistrationAsync).DisableAntiforgery();
            app.MapPost("/auth/customer/logout", LogoutAsync).DisableAntiforgery();
        }

        /// <summary>
        /// مرحله نهایی ورود با کد یکبار‌مصرف: توکن‌های صادرشده توسط API (پس از تایید موفق کد در
        /// مدار تعاملی) را می‌گیرد و کوکی احراز هویت مشتری را می‌نویسد. هیچ کد یا رمزی اینجا نیست.
        /// </summary>
        private static async Task OtpCompleteAsync(HttpContext http, GuestCartMergeService guestCartMerge)
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
            await CompleteGuestMergeAndRedirectAsync(http, guestCartMerge, accessToken, returnUrl);
        }

        private static async Task LoginAsync(HttpContext http, ApiClient apiClient, GuestCartMergeService guestCartMerge)
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
                // The outcome code travels with the redirect so the login page can offer
                // registration for an unknown mobile without matching the message text.
                http.Response.Redirect(FailUrl("/login",
                    string.IsNullOrWhiteSpace(result.Message)
                        ? "ورود ناموفق بود. شماره موبایل یا رمز عبور نادرست است."
                        : result.Message,
                    returnUrl,
                    result.ErrorCode));
                return;
            }

            await SignInCustomerAsync(http, result.Data, mobile);
            await CompleteGuestMergeAndRedirectAsync(http, guestCartMerge, result.Data.GetAccessToken(), returnUrl);
        }

        private static async Task RegisterAsync(HttpContext http, ApiClient apiClient, GuestCartMergeService guestCartMerge)
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

            // Registration no longer signs anybody in. It creates a pending account and sends a code;
            // only /auth/customer/register/verify can establish a session.
            var result = await apiClient.PostAsync<RegistrationChallengeModel>(
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
                    returnUrl,
                    result.ErrorCode));
                return;
            }

            // The mobile travels in an HttpOnly cookie rather than the query string: it is personal
            // data, and the verification step is the only thing that needs it. The password is never
            // carried anywhere - the pending account already holds it hashed.
            WritePendingRegistration(http, mobile, result.Data.MaskedMobile, returnUrl);
            http.Response.Redirect("/register?stage=verify");
        }

        /// <summary>
        /// Completes registration with the code, then establishes the session exactly as login does:
        /// same cookie writer, same guest-cart merge, same safe return url.
        /// </summary>
        private static async Task VerifyRegistrationAsync(HttpContext http, ApiClient apiClient, GuestCartMergeService guestCartMerge)
        {
            var form = await http.Request.ReadFormAsync();
            var code = form["code"].ToString().Trim();
            var pending = ReadPendingRegistration(http);

            if (pending is null)
            {
                http.Response.Redirect(FailUrl("/register", "مهلت تأیید ثبت‌نام به پایان رسید. لطفاً دوباره ثبت‌نام کنید.", string.Empty));
                return;
            }

            if (string.IsNullOrWhiteSpace(code))
            {
                http.Response.Redirect(VerifyUrl("کد تأیید را وارد کنید."));
                return;
            }

            var result = await apiClient.PostAsync<AdminLoginResponseModel>(
                "auth/register/verify",
                new { Mobile = pending.Mobile, Code = code });

            if (!result.IsSuccess || result.Data is null)
            {
                http.Response.Redirect(VerifyUrl(string.IsNullOrWhiteSpace(result.Message)
                    ? "کد تأیید معتبر نیست."
                    : result.Message));
                return;
            }

            ClearPendingRegistration(http);
            await SignInCustomerAsync(http, result.Data, pending.Mobile);
            await CompleteGuestMergeAndRedirectAsync(http, guestCartMerge, result.Data.GetAccessToken(), pending.ReturnUrl);
        }

        private static async Task ResendRegistrationAsync(HttpContext http, ApiClient apiClient)
        {
            var pending = ReadPendingRegistration(http);
            if (pending is null)
            {
                http.Response.Redirect(FailUrl("/register", "مهلت تأیید ثبت‌نام به پایان رسید. لطفاً دوباره ثبت‌نام کنید.", string.Empty));
                return;
            }

            var result = await apiClient.PostAsync<RegistrationChallengeModel>(
                "auth/register/resend", new { Mobile = pending.Mobile });

            http.Response.Redirect(result.IsSuccess
                ? VerifyUrl(null, "کد تأیید دوباره ارسال شد.")
                : VerifyUrl(string.IsNullOrWhiteSpace(result.Message) ? "ارسال مجدد کد ناموفق بود." : result.Message));
        }

        // ---------------------------------------------------------------- pending registration state

        /// <summary>
        /// The in-progress registration, held server-side in one short-lived HttpOnly cookie. Contains
        /// no secret: the password stays hashed in the pending account and never returns to the
        /// browser, and the code itself only ever travels by SMS.
        /// </summary>
        private sealed record PendingRegistration(string Mobile, string MaskedMobile, string ReturnUrl);

        private const string PendingRegistrationCookie = "vitorize-registration";

        private static void WritePendingRegistration(HttpContext http, string mobile, string? maskedMobile, string returnUrl)
        {
            var payload = string.Join('|', mobile, maskedMobile ?? string.Empty, SafeRedirect.LocalOrDefault(returnUrl, string.Empty));
            http.Response.Cookies.Append(PendingRegistrationCookie,
                Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(payload)),
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = AuthCookiePolicy.IsSecure(http),
                    SameSite = SameSiteMode.Lax,
                    // Long enough to receive an SMS and resend once; short enough that an abandoned
                    // attempt does not linger.
                    Expires = DateTimeOffset.UtcNow.AddMinutes(30),
                    Path = "/"
                });
        }

        internal static PendingRegistrationView? ReadPendingRegistrationView(HttpContext http)
        {
            var pending = ReadPendingRegistration(http);
            return pending is null ? null : new PendingRegistrationView(pending.MaskedMobile, pending.ReturnUrl);
        }

        /// <summary>What the verification page may display: the masked mobile and where to go next.</summary>
        internal sealed record PendingRegistrationView(string MaskedMobile, string ReturnUrl);

        private static PendingRegistration? ReadPendingRegistration(HttpContext http)
        {
            var raw = http.Request.Cookies[PendingRegistrationCookie];
            if (string.IsNullOrWhiteSpace(raw)) return null;
            try
            {
                var parts = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(raw)).Split('|');
                return parts.Length == 3 && !string.IsNullOrWhiteSpace(parts[0])
                    ? new PendingRegistration(parts[0], parts[1], parts[2])
                    : null;
            }
            catch { return null; }
        }

        private static void ClearPendingRegistration(HttpContext http) =>
            http.Response.Cookies.Delete(PendingRegistrationCookie, new CookieOptions { Path = "/" });

        private static string VerifyUrl(string? error, string? notice = null)
        {
            var url = "/register?stage=verify";
            if (!string.IsNullOrWhiteSpace(error)) url += $"&error={Uri.EscapeDataString(error)}";
            if (!string.IsNullOrWhiteSpace(notice)) url += $"&notice={Uri.EscapeDataString(notice)}";
            return url;
        }

        private static async Task LogoutAsync(HttpContext http, ApiClient apiClient)
        {
            var refreshToken = http.Request.Cookies[VitorizeAuthSchemes.CustomerRefreshTokenCookie];
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                // Revoke at the API before clearing browser state. The API response is deliberately
                // not reflected to the browser; logout remains safe and idempotent when a session is
                // already expired or revoked.
                await apiClient.PostAsync("auth/logout", new { RefreshToken = refreshToken });
            }
            await http.SignOutAsync(VitorizeAuthSchemes.CustomerScheme);
            // Every cookie the scheme owns, including the auth ticket. SignOutAsync normally removes
            // the ticket itself, but naming all three through the shared helper keeps this endpoint
            // and the session endpoints deleting exactly the same set.
            foreach (var cookie in VitorizeAuthSchemes.TokenCookiesFor(VitorizeAuthSchemes.CustomerScheme))
                http.Response.Cookies.Delete(cookie);
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

        private static string FailUrl(string page, string message, string returnUrl, string? errorCode = null)
        {
            var url = $"{page}?error={Uri.EscapeDataString(message)}";
            if (!string.IsNullOrWhiteSpace(returnUrl))
                url += $"&returnUrl={Uri.EscapeDataString(returnUrl)}";
            if (!string.IsNullOrWhiteSpace(errorCode))
                url += $"&code={Uri.EscapeDataString(errorCode)}";
            return url;
        }

        private static string SafeReturn(string? returnUrl) =>
            SafeRedirect.LocalOrDefault(returnUrl, "/customer/dashboard");

        private static async Task CompleteGuestMergeAndRedirectAsync(HttpContext http, GuestCartMergeService guestCartMerge, string accessToken, string returnUrl)
        {
            // The sign-in is already complete by the time the guest cart merges; a merge hiccup must
            // never change where the signed-in user lands. Redirecting to /cart?mergeError=1 here
            // used to hijack a SUCCESSFUL login into an error page - and since the guest cookie is
            // provisioned on every visit, the hijack risk applied to logins with nothing to merge at
            // all. On failure the guest cookie is kept, so the next sign-in retries the merge.
            if (await guestCartMerge.MergeAsync(http.Request.Cookies[GuestCartIdentityProvider.CookieName], accessToken)
                && !string.IsNullOrWhiteSpace(http.Request.Cookies[GuestCartIdentityProvider.CookieName]))
            {
                http.Response.Cookies.Delete(GuestCartIdentityProvider.CookieName);
            }

            http.Response.Redirect(SafeReturn(returnUrl));
        }
    }
}
