using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontWordmarkTests
{
    [Fact]
    public void Storefront_logotype_comes_from_settings_and_the_mark_stands_alone_without_one()
    {
        var root = FindSolutionRoot();
        var logo = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "StoreLogo.razor"));
        var homeLogo = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "HomeLogo.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));
        var wordmarkPath = Path.Combine(root, "Vitorize.Web", "wwwroot", "images", "vitorize-wordmark.png");

        var seed = File.ReadAllText(Path.Combine(root, "Vitorize.Infrastructure", "Services", "VitorizeSeedService.cs"));
        var branding = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Services", "UI", "StoreBrandingService.cs"));

        Assert.True(File.Exists(wordmarkPath));
        // The logotype is an uploadable setting, seeded with the file that ships so an existing
        // store keeps what it already shows.
        Assert.Contains("S(\"WordmarkPath\", \"images/vitorize-wordmark.png\", \"Logos\", \"image\"", seed, StringComparison.Ordinal);
        // Empty has to stay empty: a code-side fallback would make clearing the setting impossible.
        Assert.Contains("public string WordmarkPath => Get(\"WordmarkPath\", \"\");", branding, StringComparison.Ordinal);
        Assert.Contains("_hasWordmark = !string.IsNullOrWhiteSpace(wordmark);", logo, StringComparison.Ordinal);
        Assert.Contains("@if (ShowWord && _hasWordmark)", logo, StringComparison.Ordinal);
        // Nothing to write the name with means the mark carries it, phones included.
        Assert.Contains("st-logo--markonly", logo, StringComparison.Ordinal);
        Assert.Contains(".st-logo--markonly .st-logo__img { display: block;", css, StringComparison.Ordinal);
        Assert.Contains("class=\"st-logo__wordmark\"", logo, StringComparison.Ordinal);
        Assert.DoesNotContain("@if (_custom)", logo, StringComparison.Ordinal);
        Assert.Contains("class=\"hp-logo__wordmark\"", homeLogo, StringComparison.Ordinal);
        Assert.Contains(".site-shell .st-logo__wordmark", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .site-shell .st-logo__wordmark", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .site-shell .st-logo__img", css, StringComparison.Ordinal);
        Assert.Contains("filter: brightness(0)", css, StringComparison.Ordinal);
        // Sized by height alone. A fixed width wider than the artwork's own proportions padded the
        // picture with dead space inside its box, and that padding was the gap the design never had
        // between the mark and the word; the negative margin was an attempt to pull it back.
        Assert.Contains(".site-shell .st-logo__wordmark { display: block; width: auto; height: 22px;", css, StringComparison.Ordinal);
        Assert.DoesNotContain("margin-left: -6px", css, StringComparison.Ordinal);
        // Phones carry the logotype on its own, custom header logo or not.
        Assert.DoesNotContain(".st-logo--custom .st-logo__img { display: block;", css, StringComparison.Ordinal);
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
