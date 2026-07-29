using Microsoft.AspNetCore.Authentication;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints;

/// <summary>Terminates one browser authentication scheme after a failed token rotation.</summary>
public static class AuthSessionEndpoints
{
    public static void MapAuthSessionEndpoints(this WebApplication app)
    {
        app.MapGet("/auth/session-expired", async (HttpContext context, string? area, string? returnUrl) =>
        {
            var scheme = string.Equals(area, "admin", StringComparison.OrdinalIgnoreCase)
                ? VitorizeAuthSchemes.AdminScheme
                : VitorizeAuthSchemes.CustomerScheme;

            await context.SignOutAsync(scheme);
            foreach (var cookie in VitorizeAuthSchemes.TokenCookiesFor(scheme))
                context.Response.Cookies.Delete(cookie);

            var loginPath = scheme == VitorizeAuthSchemes.AdminScheme ? "/admin/login" : "/login";
            var destination = SafeRedirect.LocalOrDefault(returnUrl, scheme == VitorizeAuthSchemes.AdminScheme ? "/admin/dashboard" : "/customer/dashboard");
            context.Response.Redirect($"{loginPath}?returnUrl={Uri.EscapeDataString(destination)}");
        }).AllowAnonymous();
    }
}
