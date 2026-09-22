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
        Assert.Contains("css/home.css?v=20260922-6", app, StringComparison.Ordinal);
        Assert.Contains("css/site-shell.css?v=20260922-4", app, StringComparison.Ordinal);
        Assert.Contains("css/auth-page.css?v=20260920-1", app, StringComparison.Ordinal);
        Assert.Contains(".home-shell.site-shell .st-main", homeCss, StringComparison.Ordinal);
    }

    [Fact]
    public void Desktop_category_menu_keeps_root_category_titles_on_one_line()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));

        Assert.Contains("grid-template-columns: clamp(340px, 22vw, 380px) minmax(0, 1fr)", css, StringComparison.Ordinal);
        Assert.Contains(".site-shell .st-header .st-catmenu__t {", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", css, StringComparison.Ordinal);
        Assert.Contains("overflow-wrap: normal;", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Desktop_category_menu_keeps_child_and_leaf_titles_on_one_line()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));

        Assert.Contains("repeat(auto-fill, minmax(260px, 1fr))", css, StringComparison.Ordinal);
        Assert.Contains(".site-shell .st-header .st-catmenu__child > span:nth-child(2)", css, StringComparison.Ordinal);
        Assert.Contains(".site-shell .st-header .st-catmenu__leaves a", css, StringComparison.Ordinal);
        Assert.Contains("white-space: nowrap;", css, StringComparison.Ordinal);
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
