using System.Text.Json;
using Vitorize.Web.Services.UI;

namespace Vitorize.Web.Services;

public static class SeoUrlBuilder
{
    public static string Canonical(StoreBranding branding, string navigationUri, string? canonicalPath = null)
    {
        var current = new Uri(navigationUri, UriKind.Absolute);
        var configured = branding.CanonicalBaseUrl;
        var origin = Uri.TryCreate(configured, UriKind.Absolute, out var configuredUri) &&
                     configuredUri.Scheme == Uri.UriSchemeHttps
            ? new UriBuilder(Uri.UriSchemeHttps, configuredUri.Host, configuredUri.IsDefaultPort ? -1 : configuredUri.Port).Uri
            : new UriBuilder(current.Scheme, current.Host, current.IsDefaultPort ? -1 : current.Port).Uri;

        var path = canonicalPath;
        if (string.IsNullOrWhiteSpace(path)) path = current.AbsolutePath;
        path = NormalizePath(path!);
        return new Uri(origin, path).AbsoluteUri;
    }

    public static string NormalizePath(string path)
    {
        var clean = path.Split('?', '#')[0].Trim();
        if (!clean.StartsWith('/')) clean = "/" + clean;
        if (clean.Length > 1) clean = clean.TrimEnd('/');
        return clean;
    }
}

public static class SeoJsonLd
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(object value) => JsonSerializer.Serialize(value, Options)
        .Replace("</", "<\\/", StringComparison.Ordinal);

    public static string Breadcrumbs(params (string Name, string Url)[] items) => Serialize(new Dictionary<string, object?>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = items.Select((x, index) => new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = x.Name,
            ["item"] = x.Url
        }).ToList()
    });
}
