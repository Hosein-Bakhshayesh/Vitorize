using Microsoft.AspNetCore.Authentication;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints;

/// <summary>Terminates one browser authentication scheme after a failed token rotation.</summary>
public static class AuthSessionEndpoints
{
    public static void MapAuthSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/session/tokens", PersistRotatedTokensAsync)
            .RequireAuthorization()
            .DisableAntiforgery();

        // Ends one scheme in the browser's own cookie jar. Deliberately anonymous: it is reached when
        // the session is already finished, so requiring authorization would be self-defeating, and it
        // only ever deletes the caller's own cookies.
        app.MapPost("/auth/session/end", EndBrowserSessionAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

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

    private static async Task<IResult> PersistRotatedTokensAsync(HttpContext context, RotatedTokensRequest request)
    {
        var scheme = context.User.Identity?.AuthenticationType;
        if (scheme is not (VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme) ||
            !string.Equals(scheme, request.Scheme, StringComparison.Ordinal)) return Results.Forbid();

        return await AuthSessionCookieWriter.PersistAsync(context, scheme, request.AccessToken, request.RefreshToken)
            ? Results.NoContent()
            : Results.BadRequest();
    }

    private static async Task<IResult> EndBrowserSessionAsync(HttpContext context, EndSessionRequest request)
    {
        var scheme = request.Scheme is VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme
            ? request.Scheme
            : null;
        if (scheme is null) return Results.BadRequest();

        await context.SignOutAsync(scheme);
        foreach (var cookie in VitorizeAuthSchemes.TokenCookiesFor(scheme))
            context.Response.Cookies.Delete(cookie);

        return Results.NoContent();
    }

    private sealed record RotatedTokensRequest(string Scheme, string AccessToken, string RefreshToken);

    private sealed record EndSessionRequest(string Scheme);
}
