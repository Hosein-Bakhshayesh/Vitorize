using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Vitorize.Web.Services;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Guards the CKEditor 5 licensing policy: Production must fail fast without a
/// real commercial key, and non-Production may use GPL.
/// </summary>
public sealed class CkEditorLicensingTests
{
    private static IConfiguration Config(string? licenseKey)
    {
        var values = new Dictionary<string, string?>();
        if (licenseKey is not null) values["CkEditor:LicenseKey"] = licenseKey;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static IHostEnvironment Env(string environmentName) => new StubEnvironment(environmentName);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("GPL")]
    [InlineData("gpl")]
    [InlineData(" GPL ")]
    public void Production_fails_fast_without_a_commercial_key(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CkEditorOptions.Resolve(Config(key), Env(Environments.Production)));
        Assert.Contains("CkEditor__LicenseKey", ex.Message);
        Assert.Contains("Production", ex.Message);
    }

    [Fact]
    public void Production_accepts_a_non_empty_commercial_key()
    {
        var options = CkEditorOptions.Resolve(Config("commercial-key-123"), Env(Environments.Production));
        Assert.Equal("commercial-key-123", options.LicenseKey);
        Assert.False(options.IsGpl);
    }

    [Theory]
    [InlineData(null, "GPL")]
    [InlineData("", "GPL")]
    [InlineData("GPL", "GPL")]
    [InlineData("dev-commercial-key", "dev-commercial-key")]
    public void Development_allows_gpl_or_an_explicit_key(string? configured, string expected)
    {
        var options = CkEditorOptions.Resolve(Config(configured), Env(Environments.Development));
        Assert.Equal(expected, options.LicenseKey);
    }

    [Fact]
    public void Non_production_falls_back_to_gpl_but_never_production()
    {
        var staging = CkEditorOptions.Resolve(Config(null), Env("Staging"));
        Assert.True(staging.IsGpl);

        Assert.Throws<InvalidOperationException>(
            () => CkEditorOptions.Resolve(Config(null), Env(Environments.Production)));
    }

    // Regression for the read-only defect: the CDN "cloud" distribution build
    // rejects the GPL key and locks the editor read-only. The vendored build must
    // be the self-hosted ("sh") distribution, where GPL and commercial keys are
    // both valid — i.e. it must NOT carry the CDN cloud-channel injector.
    private static string ThisFile([CallerFilePath] string path = "") => path;

    [Fact]
    public void Vendored_ckeditor_build_is_the_self_hosted_distribution()
    {
        var testDir = Path.GetDirectoryName(ThisFile())!;
        var umd = Path.GetFullPath(Path.Combine(
            testDir, "..", "Vitorize.Web", "wwwroot", "lib", "ckeditor5", "ckeditor5.umd.js"));

        Assert.True(File.Exists(umd), $"Vendored CKEditor build not found at {umd}");
        var content = File.ReadAllText(umd);

        // Self-hosted default distribution channel is present…
        Assert.Contains("distribution\")]||\"sh\"", content);
        // …and the CDN build's obfuscated cloud-channel injector is absent.
        Assert.DoesNotContain("globalThis[Symbol[", content);
    }

    private sealed class StubEnvironment : IHostEnvironment
    {
        public StubEnvironment(string environmentName) => EnvironmentName = environmentName;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "Vitorize.Web.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
