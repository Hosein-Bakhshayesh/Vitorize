using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontShellConsistencyTests
{
    [Fact]
    public void Home_page_uses_the_same_header_footer_and_mobile_navigation_as_the_storefront()
    {
        var root = FindSolutionRoot();
        var layout = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Layout", "StoreLayout.razor"));
        var app = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "App.razor"));
        var homeCss = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        Assert.Contains("st-shell site-shell", layout, StringComparison.Ordinal);
        Assert.Contains("<StoreHeader />", layout, StringComparison.Ordinal);
        Assert.Contains("<StoreFooter />", layout, StringComparison.Ordinal);
        Assert.Contains("<StoreBottomNav />", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<HomeHeader", layout, StringComparison.Ordinal);
        Assert.DoesNotContain("<HomeFooter", layout, StringComparison.Ordinal);
        Assert.Contains("css/home.css?v=20260920-4", app, StringComparison.Ordinal);
        Assert.Contains(".home-shell.site-shell .st-main", homeCss, StringComparison.Ordinal);
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
