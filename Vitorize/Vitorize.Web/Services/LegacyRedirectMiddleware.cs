using Microsoft.Extensions.Caching.Memory;
using Vitorize.Web.Models.Store;

namespace Vitorize.Web.Services;

public sealed class LegacyRedirectMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ApiClient api, IMemoryCache cache)
    {
        if (!ShouldCheck(context.Request))
        {
            await next(context);
            return;
        }

        var path = SeoUrlBuilder.NormalizePath(context.Request.Path.Value ?? "/");
        var cached = await cache.GetOrCreateAsync($"legacy-redirect:{path}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var result = await api.GetAsync<LegacyRedirectModel>($"seo/redirect?path={Uri.EscapeDataString(path)}");
            return result.IsSuccess ? result.Data : null;
        });

        if (cached is null)
        {
            await next(context);
            return;
        }
        if (cached.StatusCode == StatusCodes.Status410Gone)
        {
            context.Response.StatusCode = StatusCodes.Status410Gone;
            return;
        }
        if (!string.IsNullOrWhiteSpace(cached.DestinationPath) && cached.DestinationPath != path)
        {
            context.Response.StatusCode = cached.StatusCode is 301 or 308 ? cached.StatusCode : 301;
            context.Response.Headers.Location = cached.DestinationPath;
            return;
        }
        await next(context);
    }

    private static bool ShouldCheck(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)) return false;
        var path = request.Path.Value ?? "/";
        if (Path.HasExtension(path)) return false;
        // Public content routes are intentionally not excluded: an exact registry entry
        // must be able to retire or permanently redirect an old product/category/article
        // URL even when it shares the current route prefix.
        string[] currentPrefixes = ["/admin", "/customer", "/search", "/cart", "/checkout", "/payment", "/login", "/register", "/error", "/sitemap", "/robots", "/_blazor", "/_framework"];
        return path != "/" && !currentPrefixes.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
