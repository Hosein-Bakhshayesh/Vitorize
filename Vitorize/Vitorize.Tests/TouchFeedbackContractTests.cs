using Xunit;

namespace Vitorize.Tests;

public sealed class TouchFeedbackContractTests
{
    [Fact]
    public void Interactive_controls_do_not_show_the_browser_default_blue_tap_flash()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "storefront.css"));
        var app = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "App.razor"));

        Assert.Contains(":where(a, button, summary, [role=\"button\"]) { -webkit-tap-highlight-color: transparent; }", css, StringComparison.Ordinal);
        Assert.Contains(":focus-visible", css, StringComparison.Ordinal);
        Assert.Contains("AssetVersion = \"20260920-3\"", app, StringComparison.Ordinal);
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
