using System.Text.RegularExpressions;
using FluentAssertions;
using Vitorize.Shared.Icons;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// An icon name that the catalogue does not know still renders: it silently falls back to a question
/// mark, so a typo ships as a wrong glyph rather than as a build error. These scan the components for
/// every literal icon name and assert the catalogue can resolve it.
/// </summary>
public sealed class IconNameResolutionTests
{
    // <Icon Name="x" />, <LucideIcon IconKey="x" /> and the CategoryMedia/StatCard style Icon="x".
    // Values starting with @ are Razor expressions whose content is only known at runtime.
    private static readonly Regex Literal =
        new(@"(?:<Icon\s+Name|IconKey|\sIcon)\s*=\s*""([^""@{}]+)""", RegexOptions.Compiled);

    // Tab and section registries declared in a @code block as new("key", "title", "icon", "description", …).
    // The settings page's footer tab hid a Tabler-only name here for as long as the page has existed,
    // because a scan that only understands attributes cannot see it.
    private static readonly Regex TupleRegistry =
        new(@"new\(\s*""[^""]+""\s*,\s*""[^""]+""\s*,\s*""([^""]+)""\s*,", RegexOptions.Compiled);

    [Fact]
    public void Every_icon_name_written_into_a_component_resolves()
    {
        var unresolved = new SortedDictionary<string, SortedSet<string>>();
        var seen = 0;

        foreach (var (file, name) in IconNames())
        {
            seen++;
            if (IconCatalog.IsKnown(name)) continue;

            if (!unresolved.TryGetValue(name, out var files))
                unresolved[name] = files = new SortedSet<string>();
            files.Add(file);
        }

        seen.Should().BeGreaterThan(50, "the scan should be finding the icons, not an empty folder");
        unresolved.Should().BeEmpty(
            "every icon name must resolve; unknown ones fall back to a question mark at runtime. " +
            string.Join(" | ", unresolved.Select(x => $"{x.Key} ← {string.Join(", ", x.Value)}")));
    }

    [Fact]
    public void The_fallback_every_component_relies_on_is_itself_a_real_icon()
    {
        // Icon.razor and LucideIcon.razor both default to this key; if it ever stopped resolving,
        // a typo anywhere would render nothing at all rather than a visible question mark.
        IconCatalog.IsKnown("circle-question-mark").Should().BeTrue();
    }

    private static IEnumerable<(string File, string Name)> IconNames()
    {
        var root = ComponentsRoot();
        foreach (var file in Directory.EnumerateFiles(root, "*.razor", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, file);
            var text = File.ReadAllText(file);
            foreach (var pattern in new[] { Literal, TupleRegistry })
                foreach (Match match in pattern.Matches(text))
                {
                    var name = match.Groups[1].Value.Trim();
                    if (name.Length > 0) yield return (relative, name);
                }
        }
    }

    /// <summary>Walks up from the test binary to the repository, which has no fixed depth on CI.</summary>
    private static string ComponentsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "Vitorize.Web", "Components");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Vitorize.Web/Components was not found above the test binary.");
    }
}
