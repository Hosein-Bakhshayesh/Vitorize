using Ganss.Xss;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Services;

public sealed class StrictHtmlContentSanitizer : IHtmlContentSanitizer
{
    private static readonly IReadOnlySet<string> Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "p", "br", "strong", "b", "em", "i", "u", "s", "h2", "h3", "h4",
        "blockquote", "ul", "ol", "li", "hr", "table", "thead", "tbody", "tr", "th", "td",
        "a", "img", "pre", "code", "span", "div"
    };

    private static readonly IReadOnlySet<string> Attributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "href", "title", "target", "rel", "src", "alt", "width", "height", "dir", "class"
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
        sanitizer.AllowedAtRules.Clear();
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add("http");
        sanitizer.AllowedSchemes.Add("https");

        return sanitizer.Sanitize(html.Trim());
    }
}
