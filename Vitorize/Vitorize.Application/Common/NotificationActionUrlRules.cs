using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

/// <summary>
/// FIX-15: an announcement may carry an optional call-to-action link. Only internal, relative
/// storefront paths are accepted — never a scheme, a host, or an administrative/infrastructure path.
/// </summary>
public static class NotificationActionUrlRules
{
    public const int MaximumLength = 500;

    /// <summary>First segments that must never be linked from a customer announcement.</summary>
    private static readonly string[] BlockedPrefixes =
    [
        "/admin", "/api", "/_blazor", "/_framework"
    ];

    /// <summary>
    /// Normalises and validates an administrator-supplied action URL.
    /// Returns null for an omitted link; throws <see cref="BusinessException"/> for an unsafe one.
    /// </summary>
    public static string? NormalizeInternalPath(string? actionUrl)
    {
        var value = actionUrl?.Trim();
        if (string.IsNullOrEmpty(value)) return null;

        if (value.Length > MaximumLength)
            throw new BusinessException($"لینک داخلی نمی‌تواند بیشتر از {MaximumLength} نویسه باشد.");

        // Control characters (including CR/LF) would allow header/markup smuggling.
        if (value.Any(char.IsControl))
            throw new BusinessException("لینک داخلی شامل نویسه‌های غیرمجاز است.");

        // Backslashes are normalised by some browsers into forward slashes, so "\\evil.test"
        // and "/\evil.test" must be rejected rather than repaired.
        if (value.Contains('\\'))
            throw new BusinessException("لینک داخلی معتبر نیست؛ فقط مسیرهای داخلی سایت مجاز هستند.");

        if (!value.StartsWith('/'))
            throw new BusinessException("لینک داخلی باید با / شروع شود؛ آدرس‌های خارجی مجاز نیستند.");

        // "//host" is protocol-relative and leaves the site.
        if (value.StartsWith("//", StringComparison.Ordinal))
            throw new BusinessException("لینک داخلی معتبر نیست؛ فقط مسیرهای داخلی سایت مجاز هستند.");

        // Defence in depth: anything the framework can still parse as absolute is refused.
        if (Uri.TryCreate(value, UriKind.Absolute, out _))
            throw new BusinessException("فقط مسیرهای داخلی سایت مجاز هستند.");

        var pathOnly = value.Split('?', 2)[0].Split('#', 2)[0];
        if (BlockedPrefixes.Any(prefix =>
                pathOnly.Equals(prefix, StringComparison.OrdinalIgnoreCase) ||
                pathOnly.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase)))
            throw new BusinessException("لینک داخلی نمی‌تواند به بخش مدیریت یا مسیرهای سیستمی اشاره کند.");

        return value;
    }

    /// <summary>Non-throwing check used by read-side projections and tests.</summary>
    public static bool IsSafeInternalPath(string? actionUrl)
    {
        try
        {
            NormalizeInternalPath(actionUrl);
            return true;
        }
        catch (BusinessException)
        {
            return false;
        }
    }
}
