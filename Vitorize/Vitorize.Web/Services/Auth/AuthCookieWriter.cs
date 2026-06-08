using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Vitorize.Web.Models.Auth;

namespace Vitorize.Web.Services.Auth
{
    public static class AuthCookieWriter
    {
        public static async Task SignInAsync(
            HttpContext httpContext,
            AuthResponseModel auth,
            string scheme,
            string accessTokenCookie,
            string refreshTokenCookie,
            DateTimeOffset expiresAt)
        {
            var claims = BuildClaims(auth);

            var identity = new ClaimsIdentity(claims, scheme);
            var principal = new ClaimsPrincipal(identity);

            await httpContext.SignInAsync(
                scheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = true,
                    ExpiresUtc = expiresAt
                });

            httpContext.Response.Cookies.Append(
                accessTokenCookie,
                auth.AccessToken,
                new CookieOptions
                {
                    HttpOnly = true,
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    Expires = expiresAt,
                    Path = "/"
                });

            if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
            {
                httpContext.Response.Cookies.Append(
                    refreshTokenCookie,
                    auth.RefreshToken,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = httpContext.Request.IsHttps,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(30),
                        Path = "/"
                    });
            }
        }

        public static async Task SignOutAsync(
            HttpContext httpContext,
            string scheme,
            string accessTokenCookie,
            string refreshTokenCookie)
        {
            await httpContext.SignOutAsync(scheme);

            httpContext.Response.Cookies.Delete(accessTokenCookie);
            httpContext.Response.Cookies.Delete(refreshTokenCookie);
        }

        public static bool HasAnyRole(string accessToken, params string[] roles)
        {
            var tokenRoles = GetRoles(accessToken);
            return roles.Any(role => tokenRoles.Contains(role, StringComparer.OrdinalIgnoreCase));
        }

        public static bool IsOnlyCustomer(string accessToken)
        {
            return !HasAnyRole(accessToken, "Admin", "SuperAdmin", "Support");
        }

        private static List<Claim> BuildClaims(AuthResponseModel auth)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, auth.UserId.ToString()),
                new Claim(ClaimTypes.Name, auth.FullName),
                new Claim("mobile", auth.Mobile),
                new Claim("access_token", auth.AccessToken),
                new Claim("refresh_token", auth.RefreshToken ?? string.Empty)
            };

            foreach (var role in GetRoles(auth.AccessToken))
                claims.Add(new Claim(ClaimTypes.Role, role));

            return claims;
        }

        private static List<string> GetRoles(string accessToken)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
                return new List<string>();

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);

            return jwt.Claims
                .Where(x =>
                    x.Type == ClaimTypes.Role ||
                    x.Type == "role" ||
                    x.Type == "roles" ||
                    x.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Value)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }
}
