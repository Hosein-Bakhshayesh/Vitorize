using Ganss.Xss;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// پاک‌سازی سخت‌گیرانه‌ی HTML توضیحات محصول. فقط عناصر و ویژگی‌هایی که
/// ویرایشگر CKEditor پیکربندی‌شده تولید می‌کند مجاز هستند؛ اسکریپت، iframe،
/// هندلرهای رویداد و آدرس‌های javascript: به‌طور کامل حذف می‌شوند.
/// </summary>
public sealed class StrictHtmlContentSanitizer : IHtmlContentSanitizer
{
    private static readonly IReadOnlySet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // text + structure
        "p", "br", "strong", "b", "em", "i", "u", "s", "sub", "sup",
        "h2", "h3", "h4", "blockquote", "ul", "ol", "li", "hr", "span", "div",
        // links + media
        "a", "img", "figure", "figcaption",
        // code
        "pre", "code",
        // tables (incl. CKEditor column-resize colgroup/col)
        "table", "thead", "tbody", "tr", "th", "td", "caption", "colgroup", "col"
    };

    private static readonly IReadOnlySet<string> Attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "href", "title", "target", "rel", "src", "alt", "width", "height",
        "dir", "class", "style", "colspan", "rowspan", "span"
    };

    // Only non-scripting layout properties needed for alignment and image/column resize.
    private static readonly IReadOnlySet<string> CssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "text-align", "width", "height", "float"
    };

    public string? Sanitize(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return null;
        if (html.Length > 200_000) throw new Vitorize.Shared.Exceptions.BusinessException("توضیحات کامل بیش از حد طولانی است.");

        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        foreach (var tag in Tags) sanitizer.AllowedTags.Add(tag);
        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in Attributes) sanitizer.AllowedAttributes.Add(attribute);
        sanitizer.AllowedCssProperties.Clear();
        foreach (var property in CssProperties) sanitizer.AllowedCssProperties.Add(property);
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        return sanitizer.Sanitize(html.Trim());
    }
}
