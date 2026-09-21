using Xunit;

namespace Vitorize.Tests;

public sealed class StorefrontCategoryPopoverTests
{
    [Fact]
    public void Desktop_category_children_reveal_their_leaves_on_hover_or_keyboard_focus()
    {
        var root = FindSolutionRoot();
        var header = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Storefront", "StoreHeader.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "site-shell.css"));

        Assert.Contains("st-catmenu__leaf-popover", header, StringComparison.Ordinal);
        Assert.Contains(".st-catmenu__childcol:hover .st-catmenu__leaf-popover", css, StringComparison.Ordinal);
        Assert.Contains(".st-catmenu__childcol:focus-within .st-catmenu__leaf-popover", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css, StringComparison.Ordinal);
        Assert.Contains("position: static", css, StringComparison.Ordinal);
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
