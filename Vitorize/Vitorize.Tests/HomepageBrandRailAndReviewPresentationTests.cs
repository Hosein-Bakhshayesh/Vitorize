using Xunit;

namespace Vitorize.Tests;

public sealed class HomepageBrandRailAndReviewPresentationTests
{
    [Fact]
    public void Brand_rail_moves_right_to_left_and_every_visible_copy_is_pointer_clickable()
    {
        var root = FindSolutionRoot();
        var home = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Pages", "Store", "Home.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));
        var script = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "js", "home.js"));

        Assert.Contains("class=\"hp-brandrail__sequence\"", home, StringComparison.Ordinal);
        Assert.Contains("isDuplicate ? \"hp-brandrail-clone\" : null", home, StringComparison.Ordinal);
        Assert.Contains("isDuplicate ? \"-1\" : null", home, StringComparison.Ordinal);
        Assert.Contains("Enumerable.Range(0, 2)", home, StringComparison.Ordinal);
        Assert.Contains("BrandRailRepeats", home, StringComparison.Ordinal);
        Assert.Contains("@ref=\"_brandRail\"", home, StringComparison.Ordinal);
        Assert.Contains("startBrandRail", home, StringComparison.Ordinal);
        Assert.Contains("startBrandRail", script, StringComparison.Ordinal);
        Assert.Contains("offset = (offset + elapsed * 0.054) % cycleWidth", script, StringComparison.Ordinal);
        Assert.Contains("ensureCoverage", script, StringComparison.Ordinal);
        Assert.Contains("viewport.addEventListener(\"pointerenter\", pause)", script, StringComparison.Ordinal);
        Assert.Contains("viewport.addEventListener(\"pointerleave\", resume)", script, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-start", css, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_brand_rides_the_rail_including_one_with_no_logo_uploaded()
    {
        var root = FindSolutionRoot();
        var home = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Pages", "Store", "Home.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        // The rail used to drop a brand that had no artwork. It shows the mark alone where there is
        // one, and falls back to the name where there is not, so nothing is filtered out any more.
        Assert.Contains("private List<StoreBrandModel> BrandRail => _data.Brands;", home, StringComparison.Ordinal);
        Assert.Contains("class=\"hp-brandrail__name\">@brand.Title", home, StringComparison.Ordinal);
        // A logo that fails to load leaves a drawn glyph rather than a broken-image icon.
        Assert.Contains("<StoreImage Url=\"@brand.ImagePath\"", home, StringComparison.Ordinal);
        // Nothing is drawn around the mark, and object-fit is what fits and centres it in its slot.
        // The selector outranks `.home-shell .st-img img`, which crops images to fill their frame.
        Assert.Contains(".home-shell .hp-brandrail__media .st-img img { width: 100%; height: 100%; object-fit: contain;", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".hp-brandrail__title", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Review_ratings_use_the_storefront_font_for_persian_digits()
    {
        var root = FindSolutionRoot();
        var home = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "Components", "Pages", "Store", "Home.razor"));
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        Assert.Contains("class=\"hp-review__rating\"", home, StringComparison.Ordinal);
        Assert.Contains("class=\"hp-review__avatar\"", home, StringComparison.Ordinal);
        Assert.Contains("<Icon Name=\"user\" Size=\"34\" />", home, StringComparison.Ordinal);
        Assert.Contains("font-family: Peyda", css, StringComparison.Ordinal);
        Assert.DoesNotContain(".hp-review__head b", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Dark_category_tiles_drop_the_outer_panel_and_keep_readable_text()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        Assert.Contains(":root[data-theme=\"dark\"] .home-shell .hp-category { background: transparent; border-color: transparent;", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .home-shell .hp-category__title { color: var(--st-text); }", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Dark_homepage_calls_to_action_and_reviews_use_dark_theme_tokens()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        Assert.Contains(":root[data-theme=\"dark\"] .home-shell .hp-hero__cta,", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--st-on-grad) !important;", css, StringComparison.Ordinal);
        Assert.Contains(":root[data-theme=\"dark\"] .home-shell .hp-review {", css, StringComparison.Ordinal);
        Assert.Contains(".hp-review {\n    background: transparent;\n    border-color: transparent;", css, StringComparison.Ordinal);
        Assert.Contains(".hp-review p {\n    background: var(--st-elevated);\n    color: var(--st-text-2);", css, StringComparison.Ordinal);
        Assert.Contains("filter: grayscale(1) brightness(0) invert(1);", css, StringComparison.Ordinal);
        // A hovered mark takes the brand tint on the dark page rather than reverting to artwork
        // drawn for white paper, which would leave it darker than its unhovered neighbours.
        Assert.Contains(".hp-brandrail-clone):focus-visible img {\n    filter: brightness(0) saturate(100%) invert(52%)", css, StringComparison.Ordinal);
        Assert.Contains(".hp-faq summary i { color: var(--st-primary); }", css, StringComparison.Ordinal);
        Assert.Contains(".hp-faq details[open] summary i {\n    background: color-mix", css, StringComparison.Ordinal);
        Assert.Contains("box-shadow: inset 0 0 0 1px var(--st-primary-border);", css, StringComparison.Ordinal);
        Assert.Contains(".hp-data-notice {", css, StringComparison.Ordinal);
        Assert.Contains("color: var(--sf-mint-ink);", css, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_hover_has_no_glow_in_either_theme()
    {
        var root = FindSolutionRoot();
        var css = File.ReadAllText(Path.Combine(root, "Vitorize.Web", "wwwroot", "css", "home.css"));

        Assert.Contains(".hp-category:hover .hp-category__image", css, StringComparison.Ordinal);
        Assert.DoesNotContain("#2cc3b345", css, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".hp-category:focus-visible .hp-category__image {\n    background: var(--st-primary);", css, StringComparison.Ordinal);
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
