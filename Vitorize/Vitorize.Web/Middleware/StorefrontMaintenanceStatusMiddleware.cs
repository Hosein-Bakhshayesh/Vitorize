using Microsoft.AspNetCore.Http;
using Vitorize.Web.Services.UI;

namespace Vitorize.Web.Middleware;

/// <summary>
/// Stops serving the storefront while maintenance mode is on.
///
/// This used to set a 503 status and then call the next middleware anyway, so the shop rendered and
/// worked normally underneath a 503 header — the flag looked enabled and changed nothing. It now
/// short-circuits: the request never reaches the page. The status-code-pages middleware registered
/// earlier in the pipeline re-executes <c>/error/503</c>, whose layout recognises maintenance mode and
/// renders the branded message, so there is no second maintenance page to keep in step.
///
/// What it deliberately does <b>not</b> block:
///
///   * <c>/admin</c> — an administrator must always be able to sign in and switch this back off.
///   * <c>/_blazor</c> and <c>/_framework</c> — one transport endpoint serves both the storefront and
///     the admin panel, so blocking it would take the admin UI down with the shop, including the very
///     page that disables maintenance. Purchasing is stopped in the API instead, which is where it can
///     be enforced regardless of transport.
///   * <c>/auth</c> — sign-in, needed to become the administrator who can turn this off.
///   * <c>/payment/result</c> — someone who has already paid must still see their receipt.
///   * static assets and <c>/error</c>, without which the maintenance page cannot render itself.
/// </summary>
public sealed class StorefrontMaintenanceStatusMiddleware(RequestDelegate next)
{
    private static readonly string[] AllowedPathPrefixes =
    [
        "/admin",           // the way back out of maintenance, including /admin/auth sign-in
        "/auth",            // customer logout and session cookie writes; sign-IN is blocked below
        "/api",             // not served by this host, but never this middleware's business
        "/health",
        "/payment/result",  // a completed payment must still show its receipt
        "/error",           // the maintenance page itself is re-executed through here
        "/_blazor", "/_framework", "/_content",
        "/css", "/js", "/fonts", "/images", "/uploads"
    ];

    public async Task InvokeAsync(HttpContext context, StorefrontMaintenanceService maintenance)
    {
        if (IsBlockedDuringMaintenance(context.Request))
        {
            var state = await maintenance.GetStateAsync(context.User);
            if (state is not null && !context.Response.HasStarted)
            {
                // A refused sign-in POST is sent to the home document, which renders the branded
                // maintenance page - status-code re-execution only dresses up GETs.
                if (HttpMethods.IsPost(context.Request.Method))
                {
                    context.Response.Redirect("/");
                    return;
                }

                // Short-circuit. Returning without calling next() is the whole fix: the storefront is
                // not rendered at all, and status-code-pages turns this into the branded 503.
                context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
                return;
            }
        }

        await next(context);
    }

    internal static bool IsBlockedDuringMaintenance(HttpRequest request)
    {
        // Only document navigations are decided here. Everything a customer could actually *do* -
        // add to cart, check out, pay - is an API call, and the API refuses those itself; that is what
        // stops a circuit which was already open when maintenance was switched on.
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            // With one exception: starting a NEW customer session. The /auth prefix has to stay open
            // for logout and session cookie writes, but a sign-in or registration posted during
            // maintenance used to half-succeed - token issued, cookie set - and then bounce straight
            // into the maintenance page, which read as "login is broken". It is refused outright
            // instead. Admin sign-in lives under /admin/auth and is unaffected.
            return HttpMethods.IsPost(request.Method) &&
                   (request.Path.StartsWithSegments("/auth/customer/login", StringComparison.OrdinalIgnoreCase) ||
                    request.Path.StartsWithSegments("/auth/customer/register", StringComparison.OrdinalIgnoreCase));
        }

        if (AllowedPathPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Anything with a file extension is an asset, not a page. A missing asset must stay a 404 and
        // must never be answered with a maintenance page.
        var path = request.Path.Value ?? string.Empty;
        var lastSlash = path.LastIndexOf('/');
        if (path.LastIndexOf('.') > lastSlash)
            return false;

        return true;
    }
}
