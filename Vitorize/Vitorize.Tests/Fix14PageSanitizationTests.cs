using FluentAssertions;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Services;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-14: CMS page content is rendered as <c>MarkupString</c>, so the shared
/// <see cref="StrictHtmlContentSanitizer"/> must strip every scripting vector while leaving the
/// formatting CKEditor actually produces intact.
/// </summary>
public sealed class Fix14PageSanitizationTests
{
    private readonly IHtmlContentSanitizer _sanitizer = new StrictHtmlContentSanitizer();

    [Fact]
    public void A_script_tag_is_removed()
    {
        var result = _sanitizer.Sanitize("<p>سلام</p><script>alert(1)</script>");

        result.Should().NotBeNull();
        result!.Should().NotContain("script").And.NotContain("alert(1)");
        result.Should().Contain("سلام");
    }

    [Theory]
    [InlineData("<p onclick=\"alert(1)\">x</p>", "onclick")]
    [InlineData("<img src=\"/a.png\" onerror=\"alert(1)\" />", "onerror")]
    [InlineData("<div onmouseover=\"alert(1)\">x</div>", "onmouseover")]
    public void Event_handler_attributes_are_removed(string html, string handler)
    {
        var result = _sanitizer.Sanitize(html);

        result!.Should().NotContain(handler);
    }

    [Fact]
    public void A_javascript_uri_is_removed()
    {
        var result = _sanitizer.Sanitize("<a href=\"javascript:alert(1)\">کلیک</a>");

        result!.Should().NotContain("javascript:");
        result.Should().Contain("کلیک", "the visible text survives, only the dangerous URI is dropped");
    }

    [Fact]
    public void An_iframe_is_removed()
    {
        var result = _sanitizer.Sanitize("<p>a</p><iframe src=\"https://evil.test\"></iframe>");

        result!.Should().NotContain("iframe");
    }

    [Fact]
    public void Safe_ckeditor_formatting_survives_intact()
    {
        const string html = """
            <h2>عنوان</h2><p>متن <strong>پررنگ</strong> و <em>مورب</em></p>
            <ul><li>مورد اول</li><li>مورد دوم</li></ul>
            <p><a href="https://vitorize.test/help" target="_blank" rel="noopener">راهنما</a></p>
            <figure><img src="/uploads/pages/a.png" alt="نمونه" width="600" /><figcaption>توضیح</figcaption></figure>
            <table><tbody><tr><td>سلول</td></tr></tbody></table>
            """;

        var result = _sanitizer.Sanitize(html);

        result!.Should().Contain("<h2").And.Contain("<strong").And.Contain("<em")
            .And.Contain("<ul").And.Contain("<li")
            .And.Contain("https://vitorize.test/help")
            .And.Contain("<img").And.Contain("alt=\"نمونه\"")
            .And.Contain("<figure").And.Contain("<figcaption")
            .And.Contain("<table").And.Contain("سلول");
    }

    [Fact]
    public void Sanitizing_already_sanitized_content_is_stable()
    {
        // The storefront sanitizes on read as well as on save, so the operation must be idempotent
        // and must not progressively corrupt legitimate content.
        const string html = "<h2>عنوان</h2><p>متن <strong>پررنگ</strong></p><ul><li>مورد</li></ul>";

        var once = _sanitizer.Sanitize(html);
        var twice = _sanitizer.Sanitize(once);

        twice.Should().Be(once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Blank_content_normalizes_to_null(string? html) =>
        _sanitizer.Sanitize(html).Should().BeNull();
}
