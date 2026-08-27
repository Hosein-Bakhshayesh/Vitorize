using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

/// <summary>
/// Marks the three explicit administrator-controlled markup fields as a deliberate trust boundary.
/// They may contain official provider scripts, unlike ordinary CMS content which is sanitised.
/// </summary>
public static class TrustedSiteMarkupRules
{
    private const int MaximumLength = 30_000;

    private static readonly HashSet<string> Keys = new(StringComparer.OrdinalIgnoreCase)
    {
        "TrustSeal.FooterHtml", "CustomHeadHtml", "CustomFooterHtml"
    };

    public static void ValidateSetting(string key, string? value)
    {
        if (!Keys.Contains(key)) return;
        if ((value?.Length ?? 0) > MaximumLength)
            throw new BusinessException("طول کد سفارشی نباید بیشتر از ۳۰٬۰۰۰ نویسه باشد.");
        if (value?.Contains('\0') == true)
            throw new BusinessException("کد سفارشی شامل نویسه نامعتبر است.");
    }
}
