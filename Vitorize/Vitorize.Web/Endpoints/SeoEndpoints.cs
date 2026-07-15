using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Vitorize.Web.Models.Store;
using Vitorize.Web.Services;
using Vitorize.Web.Services.UI;

namespace Vitorize.Web.Endpoints;

public static class SeoEndpoints
{
    private static readonly string[] Kinds = ["products", "categories", "brands", "blog", "pages"];
    private const int ChunkSize = 50_000;

    public static IEndpointRouteBuilder MapSeoEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/robots.txt", Robots);
        endpoints.MapGet("/sitemap.xml", SitemapIndex);
        endpoints.MapGet("/sitemaps/{kind}-{page:int}.xml", Sitemap);
        return endpoints;
    }

    private static async Task<IResult> Robots(HttpContext context, IWebHostEnvironment environment, StoreBrandingService brandingService)
    {
        if (!environment.IsProduction())
            return Results.Text("User-agent: *\nDisallow: /\n", "text/plain", Encoding.UTF8);

        var branding = await brandingService.GetAsync();
        var sitemap = SeoUrlBuilder.Canonical(branding, RequestOrigin(context), "/sitemap.xml");
        var text = "User-agent: *\nAllow: /\n" +
                   "Disallow: /admin\nDisallow: /customer\nDisallow: /cart\nDisallow: /checkout\n" +
                   "Disallow: /payment\nDisallow: /search\nDisallow: /api\n" +
                   $"Sitemap: {sitemap}\n";
        return Results.Text(text, "text/plain", Encoding.UTF8);
    }

    private static async Task<IResult> SitemapIndex(HttpContext context, ApiClient api, StoreBrandingService brandingService, IMemoryCache cache)
    {
        var branding = await brandingService.GetAsync();
        var root = new XElement(XName.Get("sitemapindex", "http://www.sitemaps.org/schemas/sitemap/0.9"));
        foreach (var kind in Kinds)
        {
            var count = await cache.GetOrCreateAsync($"sitemap-count:{kind}", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                var result = await api.GetAsync<SeoSitemapPageModel>($"seo/sitemap/{kind}?page=1&pageSize=1");
                return result.IsSuccess && result.Data is not null ? result.Data.TotalCount : 0;
            });
            var pages = Math.Max(1, (int)Math.Ceiling(count / (double)ChunkSize));
            for (var page = 1; page <= pages; page++)
            {
                root.Add(new XElement(root.Name.Namespace + "sitemap",
                    new XElement(root.Name.Namespace + "loc", SeoUrlBuilder.Canonical(branding, RequestOrigin(context), $"/sitemaps/{kind}-{page}.xml"))));
            }
        }
        return Xml(root);
    }

    private static async Task<IResult> Sitemap(string kind, int page, HttpContext context, ApiClient api, StoreBrandingService brandingService)
    {
        kind = kind.ToLowerInvariant();
        if (!Kinds.Contains(kind, StringComparer.Ordinal) || page < 1) return Results.NotFound();
        var result = await api.GetAsync<SeoSitemapPageModel>($"seo/sitemap/{kind}?page={page}&pageSize={ChunkSize}");
        if (!result.IsSuccess || result.Data is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (page > 1 && result.Data.Items.Count == 0) return Results.NotFound();

        var branding = await brandingService.GetAsync();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var root = new XElement(ns + "urlset");
        if (kind == "pages" && page == 1)
        {
            foreach (var path in new[] { "/", "/shop", "/categories", "/blog", "/faq" })
                root.Add(Url(ns, SeoUrlBuilder.Canonical(branding, RequestOrigin(context), path), null));
        }
        foreach (var item in result.Data.Items)
            root.Add(Url(ns, SeoUrlBuilder.Canonical(branding, RequestOrigin(context), item.Path), item.LastModified));
        return Xml(root);
    }

    private static XElement Url(XNamespace ns, string location, DateTime? modified) =>
        new(ns + "url",
            new XElement(ns + "loc", location),
            modified.HasValue ? new XElement(ns + "lastmod", modified.Value.ToUniversalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) : null);

    private static IResult Xml(XElement root)
    {
        var document = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
        return Results.Text(document.ToString(SaveOptions.DisableFormatting), "application/xml", Encoding.UTF8,
            StatusCodes.Status200OK);
    }

    private static string RequestOrigin(HttpContext context) =>
        $"{context.Request.Scheme}://{context.Request.Host}/";
}
