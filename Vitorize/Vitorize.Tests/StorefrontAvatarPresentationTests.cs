using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontAvatarPresentationTests
{
    [Fact]
    public void Signed_in_header_avatar_is_a_theme_aware_circle()
    {
        var root = FindSolutionRoot();
        var header = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "StoreHeader.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));

        Assert.Contains("<button class=\"st-avatar\"", header, StringComparison.Ordinal);
        Assert.Contains("border-radius: 50%", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--st-primary-soft)", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--st-primary)", css, StringComparison.Ordinal);
        Assert.Contains("background: var(--st-primary)", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--st-on-primary)", css, StringComparison.Ordinal);
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
