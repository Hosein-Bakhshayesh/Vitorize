using System.Text.Json;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Icons;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Regression coverage for the CKEditor 5 integration (sanitizer round-trip),
/// the multi-collection icon catalog (backward compatibility) and the unchanged
/// SEO API contract.
/// </summary>
public sealed class CkEditorSanitizerAndIconTests
{
    private static string Sanitize(string html) => new StrictHtmlContentSanitizer().Sanitize(html) ?? string.Empty;

    // ---------- Sanitizer: CKEditor output round-trips ----------

    [Fact]
    public void Sanitizer_keeps_ckeditor_image_figure_with_alt_and_alignment()
    {
        var html = "<figure class=\"image image-style-align-center image_resized\" style=\"width:50%\">" +
                   "<img src=\"https://cdn.example.com/uploads/products/a.png\" alt=\"توضیح تصویر\">" +
                   "<figcaption>عنوان</figcaption></figure>";
        var result = Sanitize(html);

        Assert.Contains("<figure", result);
        Assert.Contains("figcaption", result);
        Assert.Contains("alt=\"توضیح تصویر\"", result);
        Assert.Contains("image-style-align-center", result);
        Assert.Contains("width:50%", result.Replace(" ", string.Empty));
        Assert.Contains("src=\"https://cdn.example.com/uploads/products/a.png\"", result);
    }

    [Fact]
    public void Sanitizer_keeps_table_code_block_and_text_alignment()
    {
        var html = "<figure class=\"table\"><table><thead><tr><th>سر</th></tr></thead>" +
                   "<tbody><tr><td colspan=\"2\">خانه</td></tr></tbody></table></figure>" +
                   "<pre><code class=\"language-plaintext\">code()</code></pre>" +
                   "<p style=\"text-align:center\">وسط<sub>۲</sub><sup>۳</sup></p>";
        var result = Sanitize(html);

        Assert.Contains("<table>", result);
        Assert.Contains("colspan=\"2\"", result);
        Assert.Contains("<pre>", result);
        Assert.Contains("<code", result);
        Assert.Contains("text-align:center", result.Replace(" ", string.Empty));
        Assert.Contains("<sub>", result);
        Assert.Contains("<sup>", result);
    }

    [Theory]
    [InlineData("<figure class=\"image\"><img src=\"x\" onerror=\"alert(1)\"></figure>", "onerror")]
    [InlineData("<figure onclick=\"steal()\">x</figure>", "onclick")]
    [InlineData("<p style=\"width:expression(alert(1))\">x</p>", "expression")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript:")]
    [InlineData("<script>alert(1)</script><p>ok</p>", "<script")]
    [InlineData("<iframe src=\"https://evil\"></iframe>", "<iframe")]
    public void Sanitizer_strips_dangerous_constructs_from_editor_html(string html, string forbidden)
    {
        var result = Sanitize(html);
        Assert.DoesNotContain(forbidden, result, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitizer_preserves_persian_and_english_formatted_content()
    {
        var html = "<h2>عنوان فارسی</h2><p><strong>English</strong> و <em>فارسی</em></p>" +
                   "<ul><li>مورد ۱</li></ul><blockquote>نقل‌قول</blockquote>";
        var result = Sanitize(html);

        Assert.Contains("عنوان فارسی", result);
        Assert.Contains("<strong>English</strong>", result);
        Assert.Contains("<blockquote>", result);
        Assert.Contains("<li>مورد ۱</li>", result);
    }

    // ---------- Icon catalog: backward compatibility + collections ----------

    [Fact]
    public void Icon_catalog_exposes_lucide_plus_extra_collections()
    {
        Assert.Contains(IconCatalog.Collections, c => c.Prefix == "lucide");
        Assert.Contains(IconCatalog.Collections, c => c.Prefix == "tabler");
        Assert.Contains(IconCatalog.Collections, c => c.Prefix == "ph");
        Assert.All(IconCatalog.Collections, c => Assert.True(c.Count > 0));
    }

    [Theory]
    [InlineData("wallet")]          // bare Lucide key (existing stored format)
    [InlineData("gamepad-2")]
    [InlineData("shopping-cart")]
    public void Legacy_bare_lucide_values_still_resolve_to_lucide_sprite(string value)
    {
        var render = IconCatalog.Resolve(value);
        Assert.True(render.Found);
        Assert.Equal("/lib/lucide/lucide-sprite.svg", render.SpritePath);
        Assert.Equal(value, render.SymbolId);
    }

    [Theory]
    [InlineData("cart", "shopping-cart")]   // legacy alias mapping preserved
    [InlineData("home", "house")]
    [InlineData("edit", "pencil")]
    public void Legacy_alias_values_still_map_through_lucide(string value, string expected)
    {
        var render = IconCatalog.Resolve(value);
        Assert.True(render.Found);
        Assert.Equal(expected, render.SymbolId);
    }

    [Fact]
    public void Namespaced_tabler_and_phosphor_values_resolve_to_their_sprites()
    {
        var tabler = IconCatalog.Resolve("tabler:brand-steam");
        Assert.True(tabler.Found);
        Assert.Equal("/lib/icons/tabler-sprite.svg", tabler.SpritePath);
        Assert.Equal("brand-steam", tabler.SymbolId);

        var phosphor = IconCatalog.Resolve("ph:game-controller-fill");
        Assert.True(phosphor.Found);
        Assert.Equal("/lib/icons/ph-sprite.svg", phosphor.SpritePath);
    }

    [Theory]
    [InlineData("tabler:does-not-exist")]
    [InlineData("totally-not-an-icon")]
    [InlineData("unknownprefix:foo")]
    public void Unknown_values_fall_back_safely_without_being_marked_found(string value)
    {
        var render = IconCatalog.Resolve(value);
        Assert.False(render.Found);
        Assert.Equal("/lib/lucide/lucide-sprite.svg", render.SpritePath);
        Assert.False(string.IsNullOrWhiteSpace(render.SymbolId));
    }

    [Fact]
    public void TryParse_treats_bare_values_as_lucide_and_known_prefixes_as_namespaced()
    {
        Assert.True(IconCatalog.TryParse("wallet", out var p1, out var n1));
        Assert.Equal("lucide", p1);
        Assert.Equal("wallet", n1);

        Assert.True(IconCatalog.TryParse("tabler:coin", out var p2, out var n2));
        Assert.Equal("tabler", p2);
        Assert.Equal("coin", n2);

        // Unknown prefix is not split — kept as a (legacy) Lucide name.
        Assert.True(IconCatalog.TryParse("foo:bar", out var p3, out var n3));
        Assert.Equal("lucide", p3);
        Assert.Equal("foo:bar", n3);
    }

    [Fact]
    public void Search_returns_matches_by_name_and_keyword_across_collections()
    {
        var steam = IconCatalog.Search("steam", "tabler", 50);
        Assert.Contains(steam, x => x.Id == "tabler:brand-steam");

        var walletAll = IconCatalog.Search("wallet", null, 200);
        Assert.NotEmpty(walletAll);

        var empty = IconCatalog.Search("zzzzz-no-such-icon", "tabler", 50);
        Assert.Empty(empty);

        // Empty query on a collection lists it (non-empty) rather than freezing.
        var browse = IconCatalog.Search("", "tabler", 500);
        Assert.NotEmpty(browse);
    }

    [Fact]
    public void Find_returns_display_metadata_for_both_formats()
    {
        Assert.Equal("lucide", IconCatalog.Find("wallet")!.Prefix);
        Assert.Equal("tabler", IconCatalog.Find("tabler:brand-steam")!.Prefix);
        Assert.Null(IconCatalog.Find("tabler:missing"));
    }

    // ---------- SEO API contract is unchanged (labels only changed in the UI) ----------

    [Fact]
    public void Product_request_contract_keeps_seo_property_names()
    {
        var dto = new CreateProductRequestDto
        {
            Slug = "s",
            SeoTitle = "t",
            SeoDescription = "d",
            FocusKeyword = "k"
        };

        var json = JsonSerializer.Serialize(dto);
        Assert.Contains("\"Slug\"", json);
        Assert.Contains("\"SeoTitle\"", json);
        Assert.Contains("\"SeoDescription\"", json);
        Assert.Contains("\"FocusKeyword\"", json);
    }
}
