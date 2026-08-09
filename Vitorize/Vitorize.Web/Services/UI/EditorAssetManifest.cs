using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace Vitorize.Web.Services.UI;

/// <summary>Content-hashed URLs for the self-hosted editor asset contract.</summary>
public sealed class EditorAssetManifest
{
    private readonly IWebHostEnvironment _environment;
    private readonly ConcurrentDictionary<string, string> _urls = new(StringComparer.Ordinal);

    public EditorAssetManifest(IWebHostEnvironment environment) => _environment = environment;

    public EditorAssetUrls GetUrls() => new(
        UrlFor("js/editor-loader.js"),
        UrlFor("lib/ckeditor5/ckeditor5.umd.js"),
        UrlFor("lib/ckeditor5/translations/fa.umd.js"),
        UrlFor("js/ckeditor-interop.js"),
        UrlFor("lib/ckeditor5/ckeditor5.css"),
        UrlFor("css/ckeditor-theme.css"));

    private string UrlFor(string relativePath) => _urls.GetOrAdd(relativePath, path =>
    {
        var file = _environment.WebRootFileProvider.GetFileInfo(path);
        if (!file.Exists) throw new FileNotFoundException($"Editor asset was not found: {path}", path);
        using var stream = file.CreateReadStream();
        var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        return $"/{path}?v={hash}";
    });
}

public sealed record EditorAssetUrls(
    string Loader,
    string Bundle,
    string Translation,
    string Interop,
    string BundleCss,
    string ThemeCss);
