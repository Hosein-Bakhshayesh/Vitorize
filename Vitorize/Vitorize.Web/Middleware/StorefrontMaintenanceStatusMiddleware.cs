using Microsoft.AspNetCore.Http;
using Vitorize.Web.Services.UI;

namespace Vitorize.Web.Middleware;

/// <summary>
/// Applies HTTP 503 semantics to direct storefront-document requests while the
/// same maintenance state is rendered by <see cref="Components.Layout.StoreLayout"/>.
/// It deliberately runs after static files and authentication, but before the
/// Razor-components endpoint can start the response body.
/// </summary>
public sealed class StorefrontMaintenanceStatusMiddleware(RequestDelegate next)
{
    private static readonly string[] ExemptPathPrefixes =
    [
        "/admin", "/customer", "/api", "/auth", "/health",
        "/_blazor", "/_framework", "/_content", "/css", "/js", "/fonts", "/images", "/uploads"
    ];

    public async Task InvokeAsync(HttpContext context, StorefrontMaintenanceService maintenance)
    {
        if (IsStorefrontDocumentRequest(context.Request))
        {
            var state = await maintenance.GetStateAsync(context.User);
            if (state is not null)
            {
                // This middleware is deliberately placed before endpoint execution.
                // Guarding this keeps the contract safe if its order changes later.
                if (!context.Response.HasStarted)
                    context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            }
        }

        await next(context);
    }

    internal static bool IsStorefrontDocumentRequest(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
            return false;

        // Enhanced Blazor navigation updates an already-open interactive document.
        // It has no standalone document-status contract and continues to let the
        // layout render the maintenance state without forcing a reload.
        if (request.Headers.ContainsKey("blazor-enhanced-nav"))
            return false;

        var path = request.Path.Value ?? string.Empty;
        if (ExemptPathPrefixes.Any(prefix => request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase)))
            return false;

        // All owned static/SEO asset paths have an extension; this is a second
        // defence after UseStaticFiles so a missing asset cannot become a 503.
        var lastSlash = path.LastIndexOf('/');
        if (path.LastIndexOf('.') > lastSlash)
            return false;

        return true;
    }
}
