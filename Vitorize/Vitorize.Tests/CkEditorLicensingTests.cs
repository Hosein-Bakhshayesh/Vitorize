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
    private static IConfiguration Config(string? licenseKey, string? allowGplInProduction = null)
    {
        var values = new Dictionary<string, string?>();
        if (licenseKey is not null) values["CkEditor:LicenseKey"] = licenseKey;
        if (allowGplInProduction is not null) values["CkEditor:AllowGplInProduction"] = allowGplInProduction;
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

    // ---- Production combinations for the temporary GPL-in-Production override ----

    [Theory]
    [InlineData("GPL", "true")]
    [InlineData("gpl", "true")]
    [InlineData(" GPL ", "True")]
    public void Production_allows_gpl_only_when_explicitly_opted_in(string key, string allow)
    {
        var options = CkEditorOptions.Resolve(Config(key, allow), Env(Environments.Production));
        Assert.True(options.IsGpl);
        Assert.True(options.IsGplInProduction);
    }

    [Theory]
    [InlineData("GPL", "false")]
    [InlineData("GPL", "FALSE")]
    [InlineData("GPL", "not-a-bool")]
    [InlineData("GPL", null)]        // AllowGplInProduction absent -> default false
    public void Production_rejects_gpl_without_opt_in(string key, string? allow)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CkEditorOptions.Resolve(Config(key, allow), Env(Environments.Production)));
        Assert.Contains("CkEditor__AllowGplInProduction", ex.Message);
    }

    [Theory]
    [InlineData(null, "true")]
    [InlineData("", "true")]
    [InlineData("   ", "true")]
    public void Production_still_rejects_empty_key_even_with_opt_in(string? key, string allow)
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => CkEditorOptions.Resolve(Config(key, allow), Env(Environments.Production)));
        Assert.Contains("CkEditor__LicenseKey", ex.Message);
    }

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData(null)]
    public void Production_commercial_key_ignores_the_opt_in_flag(string? allow)
    {
        var options = CkEditorOptions.Resolve(Config("commercial-key-123", allow), Env(Environments.Production));
        Assert.Equal("commercial-key-123", options.LicenseKey);
        Assert.False(options.IsGpl);
        Assert.False(options.IsGplInProduction);   // never flagged for a commercial key
    }

    [Fact]
    public void Development_gpl_is_never_flagged_as_gpl_in_production()
    {
        var options = CkEditorOptions.Resolve(Config("GPL", "true"), Env(Environments.Development));
        Assert.True(options.IsGpl);
        Assert.False(options.IsGplInProduction);   // opt-in flag only applies to Production
    }

    [Fact]
    public void Gpl_in_production_warning_message_is_exact()
    {
        Assert.Equal(
            "CKEditor 5 is running in GPL mode in Production. Ensure the application complies with the applicable GPL license obligations.",
            CkEditorOptions.GplInProductionWarning);
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
