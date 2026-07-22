using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Hosting;

namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// Central, environment-aware policy for the <c>Secure</c> attribute of the auth cookies.
    ///
    /// Production always requires <c>Secure</c>. Development mirrors the request (<c>SameAsRequest</c>)
    /// so the app works over BOTH the HTTP and HTTPS launch profiles: a <c>Secure</c> cookie is
    /// silently dropped by browsers when set over plain HTTP on a non-localhost host, which breaks the
    /// post-login redirect (the browser accepts the response but never persists/sends the cookie, so
    /// the redirected request is anonymous and bounces back to the login page). This never disables
    /// <c>Secure</c> in Production and never touches HttpOnly/SameSite.
    /// </summary>
    public static class AuthCookiePolicy
    {
        public static CookieSecurePolicy SecurePolicy(IHostEnvironment environment) =>
            environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;

        /// <summary>
        /// The <c>Secure</c> flag for cookies written directly via <c>Response.Cookies.Append</c>,
        /// kept consistent with <see cref="SecurePolicy"/>: always secure in Production; in
        /// Development secure only when the current request is HTTPS.
        /// </summary>
        public static bool IsSecure(HttpContext http) =>
            IsSecure(http.RequestServices.GetRequiredService<IHostEnvironment>(), http.Request.IsHttps);

        public static bool IsSecure(IHostEnvironment environment, bool requestIsHttps) =>
            !environment.IsDevelopment() || requestIsHttps;
    }
}
