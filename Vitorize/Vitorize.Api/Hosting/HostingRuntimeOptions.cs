using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Vitorize.Api.Hosting;

public sealed class HostingRuntimeOptions
{
    public const string SectionName = "Hosting";
    public string PublicOrigin { get; set; } = string.Empty;
    public string DataProtectionKeysPath { get; set; } = string.Empty;
    public string DataProtectionApplicationName { get; set; } = "Vitorize";
    public string PublicMediaRoot { get; set; } = string.Empty;
    public string PrivateDocumentsRoot { get; set; } = string.Empty;
    public List<string> TrustedProxies { get; set; } = [];
    public List<string> TrustedProxyNetworks { get; set; } = [];
}

public sealed class HostingStoragePaths
{
    private readonly IWebHostEnvironment _environment;
    private readonly HostingRuntimeOptions _options;

    public HostingStoragePaths(IWebHostEnvironment environment, IConfiguration configuration)
    {
        _environment = environment;
        _options = configuration.GetSection(HostingRuntimeOptions.SectionName).Get<HostingRuntimeOptions>() ?? new HostingRuntimeOptions();
        PublicMediaRoot = Resolve(_options.PublicMediaRoot, "wwwroot", "uploads");
        PrivateDocumentsRoot = Resolve(_options.PrivateDocumentsRoot, "private", "verification-documents");
        DataProtectionKeysPath = Resolve(_options.DataProtectionKeysPath, "data-protection-keys");
    }

    public string PublicMediaRoot { get; }
    public string PrivateDocumentsRoot { get; }
    public string DataProtectionKeysPath { get; }
    public string DataProtectionApplicationName => _options.DataProtectionApplicationName.Trim();
    public IReadOnlyList<string> TrustedProxies => _options.TrustedProxies;
    public IReadOnlyList<string> TrustedProxyNetworks => _options.TrustedProxyNetworks;

    public void ValidateAndPrepare()
    {
        if (_environment.IsProduction())
        {
            if (!IsHttpsOrigin(_options.PublicOrigin)) throw new InvalidOperationException("Hosting:PublicOrigin must be an absolute HTTPS origin in Production.");
            if (string.IsNullOrWhiteSpace(_options.DataProtectionKeysPath) || string.IsNullOrWhiteSpace(_options.PublicMediaRoot) || string.IsNullOrWhiteSpace(_options.PrivateDocumentsRoot))
                throw new InvalidOperationException("Production requires persistent Hosting data-protection, public-media, and private-documents roots.");
            if (string.IsNullOrWhiteSpace(DataProtectionApplicationName)) throw new InvalidOperationException("Hosting:DataProtectionApplicationName is required in Production.");
            if (TrustedProxies.Count == 0 && TrustedProxyNetworks.Count == 0) throw new InvalidOperationException("Production requires at least one trusted reverse proxy or network.");
        }
        if (string.Equals(PublicMediaRoot, PrivateDocumentsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Public media and private document roots must be separate.");
        EnsureWritable(DataProtectionKeysPath, "data-protection key"); EnsureWritable(PublicMediaRoot, "public-media"); EnsureWritable(PrivateDocumentsRoot, "private-document");
    }

    public void ConfigureForwardedHeaders(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 2;
        foreach (var proxy in TrustedProxies) { if (!IPAddress.TryParse(proxy, out var address)) throw new InvalidOperationException("Hosting:TrustedProxies contains an invalid IP address."); options.KnownProxies.Add(address); }
        foreach (var network in TrustedProxyNetworks) { if (!Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(network, out var parsed)) throw new InvalidOperationException("Hosting:TrustedProxyNetworks contains an invalid CIDR network."); options.KnownNetworks.Add(parsed); }
    }

    private string Resolve(string configuredPath, params string[] fallback) => Path.GetFullPath(string.IsNullOrWhiteSpace(configuredPath) ? Path.Combine(_environment.ContentRootPath, Path.Combine(fallback)) : configuredPath.Trim());
    private static bool IsHttpsOrigin(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) && string.IsNullOrEmpty(uri.Query) && string.IsNullOrEmpty(uri.Fragment);
    private static void EnsureWritable(string path, string purpose) { Directory.CreateDirectory(path); var probe = Path.Combine(path, $".vitorize-{purpose}-{Guid.NewGuid():N}.probe"); using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { } }
}
