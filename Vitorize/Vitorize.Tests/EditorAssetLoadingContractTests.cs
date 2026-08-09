using Xunit;

namespace Vitorize.Tests;

public sealed class EditorAssetLoadingContractTests
{
    [Fact]
    public void Editor_assets_are_owned_by_the_component_not_the_initial_route()
    {
        var root = FindSolutionRoot();
        var app = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "App.razor"));
        var component = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Shared", "RichTextEditor.razor"));
        var loader = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "js", "editor-loader.js"));

        Assert.DoesNotContain("_needsEditorAssets", app, StringComparison.Ordinal);
        Assert.Contains("EditorAssets.GetUrls().Loader", component, StringComparison.Ordinal);
        Assert.Contains("window[stateKey]", loader, StringComparison.Ordinal);
        Assert.Contains("await addScript(assets.bundle)", loader, StringComparison.Ordinal);
        Assert.Contains("await addScript(assets.interop)", loader, StringComparison.Ordinal);
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
