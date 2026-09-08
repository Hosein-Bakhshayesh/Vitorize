using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Endpoints;

/// <summary>Terminates one browser authentication scheme after a failed token rotation.</summary>
public static class AuthSessionEndpoints
{
    public static void MapAuthSessionEndpoints(this WebApplication app)
    {
        app.MapPost("/auth/session/tokens", PersistRotatedTokensAsync)
            // This endpoint serves both browser areas.  The default smart scheme treats
            // /auth/... as customer-facing, which meant an administrator's perfectly valid
            // cookie was never considered here and a rotated admin token could not be saved.
            // Ask authorization to authenticate either explicit area instead; the handler then
            // verifies that the requested scheme itself owns a valid ticket before it writes it.
            .RequireAuthorization(new AuthorizeAttribute
            {
                AuthenticationSchemes = $"{VitorizeAuthSchemes.AdminScheme},{VitorizeAuthSchemes.CustomerScheme}"
            })
            .DisableAntiforgery();

        // Ends one scheme in the browser's own cookie jar. Deliberately anonymous: it is reached when
        // the session is already finished, so requiring authorization would be self-defeating, and it
        // only ever deletes the caller's own cookies.
        app.MapPost("/auth/session/end", EndBrowserSessionAsync)
            .AllowAnonymous()
            .DisableAntiforgery();

        // Anonymous by necessity - the session it reports on is already gone - but the area is only a
        // hint from the query string, so it must not be trusted to decide what gets signed out. A link
        // to ?area=admin from anywhere would otherwise end an administrator's session. The requested
        // area is honoured only when the caller actually holds that area's cookie; otherwise the page
        // still renders and redirects, it just does not sign anything out.
        app.MapGet("/auth/session-expired", async (HttpContext context, string? area, string? returnUrl) =>
        {
            var scheme = string.Equals(area, "admin", StringComparison.OrdinalIgnoreCase)
                ? VitorizeAuthSchemes.AdminScheme
                : VitorizeAuthSchemes.CustomerScheme;

            var ownsRequestedArea = context.Request.Cookies.ContainsKey(
                scheme == VitorizeAuthSchemes.AdminScheme
                    ? VitorizeAuthSchemes.AdminAuthCookie
                    : VitorizeAuthSchemes.CustomerAuthCookie);

            if (!ownsRequestedArea)
            {
                var safeDestination = SafeRedirect.LocalOrDefault(returnUrl, scheme == VitorizeAuthSchemes.AdminScheme ? "/admin/dashboard" : "/customer/dashboard");
                var safeLogin = scheme == VitorizeAuthSchemes.AdminScheme ? "/admin/login" : "/login";
                context.Response.Redirect($"{safeLogin}?returnUrl={Uri.EscapeDataString(safeDestination)}");
                return;
            }

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
        if (request.Scheme is not (VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme))
            return Results.BadRequest();

        // Do not infer the area from the route or the default identity: this route is deliberately
        // shared.  Authenticate the exact scheme requested by the already-running circuit instead.
        var ticket = await context.AuthenticateAsync(request.Scheme);
        if (!ticket.Succeeded || ticket.Principal?.Identity?.IsAuthenticated != true)
            return Results.Forbid();

        return await AuthSessionCookieWriter.PersistAsync(context, request.Scheme, request.AccessToken, request.RefreshToken)
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
