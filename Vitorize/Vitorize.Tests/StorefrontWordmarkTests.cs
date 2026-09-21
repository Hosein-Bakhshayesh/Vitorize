using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontWordmarkTests
{
    [Fact]
    public void Storefront_logos_always_use_the_packaged_English_wordmark()
    {
        var root = FindSolutionRoot();
        var logo = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "StoreLogo.razor"));
        var homeLogo = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "HomeLogo.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));
        var wordmarkPath = Path.Combine(root, "Vitorize.Web", "wwwroot", "images", "vitorize-wordmark.png");

        Assert.True(File.Exists(wordmarkPath));
        Assert.Contains("DefaultWordmark = \"images/vitorize-wordmark.png\"", logo, StringComparison.Ordinal);
        Assert.Contains("class=\"st-logo__wordmark\"", logo, StringComparison.Ordinal);
        Assert.DoesNotContain("@if (_custom)", logo, StringComparison.Ordinal);
        Assert.Contains("class=\"hp-logo__wordmark\"", homeLogo, StringComparison.Ordinal);
        Assert.Contains(".site-shell .st-logo__wordmark", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .site-shell .st-logo__wordmark", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .site-shell .st-logo__img", css, StringComparison.Ordinal);
        Assert.Contains("filter: brightness(0)", css, StringComparison.Ordinal);
        Assert.Contains("margin-left: -6px", css, StringComparison.Ordinal);
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Vitorize.sln"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the Vitorize solution root.");
    }
}
