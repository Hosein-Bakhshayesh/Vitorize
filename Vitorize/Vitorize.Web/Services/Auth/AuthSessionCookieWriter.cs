using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace Vitorize.Web.Services.Auth;

/// <summary>Updates the authentication ticket and HttpOnly token cookies as one session operation.</summary>
public static class AuthSessionCookieWriter
{
    public static async Task<bool> PersistAsync(HttpContext context, string scheme, string accessToken, string refreshToken)
    {
        if (scheme is not (VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme) ||
            string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(refreshToken)) return false;

        var ticket = await context.AuthenticateAsync(scheme);
        if (!ticket.Succeeded || ticket.Principal?.Identity?.IsAuthenticated != true) return false;

        var claims = ticket.Principal.Claims
            .Where(claim => claim.Type is not "access_token" and not "refresh_token" and not ClaimTypes.Role and not "permission")
            .Append(new Claim("access_token", accessToken))
            .Append(new Claim("refresh_token", refreshToken))
            .Concat(JwtHelper.ExtractRoles(accessToken).Select(role => new Claim(ClaimTypes.Role, role)))
            .Concat(JwtHelper.ExtractPermissions(accessToken).Select(permission => new Claim("permission", permission)));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, scheme));
        var expiry = ticket.Properties.ExpiresUtc ?? DateTimeOffset.UtcNow.Add(
            scheme == VitorizeAuthSchemes.AdminScheme ? TimeSpan.FromHours(8) : TimeSpan.FromDays(14));

        await context.SignInAsync(scheme, principal, ticket.Properties);

        var baseOptions = new CookieOptions { HttpOnly = true, Secure = AuthCookiePolicy.IsSecure(context), SameSite = SameSiteMode.Lax, Expires = expiry, Path = "/" };
        var accessCookie = scheme == VitorizeAuthSchemes.AdminScheme ? VitorizeAuthSchemes.AdminAccessTokenCookie : VitorizeAuthSchemes.CustomerAccessTokenCookie;
        var refreshCookie = scheme == VitorizeAuthSchemes.AdminScheme ? VitorizeAuthSchemes.AdminRefreshTokenCookie : VitorizeAuthSchemes.CustomerRefreshTokenCookie;
        context.Response.Cookies.Append(accessCookie, accessToken, baseOptions);
        var refreshExpiry = scheme == VitorizeAuthSchemes.AdminScheme && expiry <= DateTimeOffset.UtcNow.AddHours(8).AddMinutes(1) ? expiry : DateTimeOffset.UtcNow.AddDays(30);
        context.Response.Cookies.Append(refreshCookie, refreshToken, new CookieOptions { HttpOnly = true, Secure = baseOptions.Secure, SameSite = baseOptions.SameSite, Expires = refreshExpiry, Path = "/" });
        return true;
    }
}
