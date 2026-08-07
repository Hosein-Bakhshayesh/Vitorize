using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace Vitorize.Api.Hosting;

public sealed class HostingRuntimeOptions
{
    public const string SectionName = "Hosting";
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
        var dataRoot = Path.Combine(_environment.ContentRootPath, "App_Data");
        PublicMediaRoot = Path.Combine(dataRoot, "PublicMedia");
        PrivateDocumentsRoot = Path.Combine(dataRoot, "PrivateDocuments");
        DataProtectionKeysPath = Path.Combine(dataRoot, "DataProtection");
    }

    public string PublicMediaRoot { get; }
    public string PrivateDocumentsRoot { get; }
    public string DataProtectionKeysPath { get; }
    public IReadOnlyList<string> TrustedProxies => _options.TrustedProxies;
    public IReadOnlyList<string> TrustedProxyNetworks => _options.TrustedProxyNetworks;

    public void ValidateAndPrepare()
    {
        if (string.Equals(PublicMediaRoot, PrivateDocumentsRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Public media and private document roots must be separate.");
        EnsureWritable(DataProtectionKeysPath, "data-protection key"); EnsureWritable(PublicMediaRoot, "public-media"); EnsureWritable(PrivateDocumentsRoot, "private-document");
    }

    public void ConfigureForwardedHeaders(ForwardedHeadersOptions options)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost;
        options.ForwardLimit = 2;
        options.KnownProxies.Add(IPAddress.Loopback);
        options.KnownProxies.Add(IPAddress.IPv6Loopback);
        foreach (var proxy in TrustedProxies) { if (!IPAddress.TryParse(proxy, out var address)) throw new InvalidOperationException("Hosting:TrustedProxies contains an invalid IP address."); options.KnownProxies.Add(address); }
        foreach (var network in TrustedProxyNetworks) { if (!Microsoft.AspNetCore.HttpOverrides.IPNetwork.TryParse(network, out var parsed)) throw new InvalidOperationException("Hosting:TrustedProxyNetworks contains an invalid CIDR network."); options.KnownNetworks.Add(parsed); }
    }

    private static void EnsureWritable(string path, string purpose) { Directory.CreateDirectory(path); var probe = Path.Combine(path, $".vitorize-{purpose}-{Guid.NewGuid():N}.probe"); using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { } }
}
