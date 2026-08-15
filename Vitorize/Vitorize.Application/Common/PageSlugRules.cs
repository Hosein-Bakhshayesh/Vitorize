using Vitorize.Shared.Exceptions;

namespace Vitorize.Application.Common;

/// <summary>
/// FIX-14: the single slug contract for CMS pages. Custom pages are published under
/// <c>/page/{slug}</c> and system pages under their own short route, so a slug may never collide
/// with a real storefront/admin route.
/// </summary>
public static class PageSlugRules
{
    public const int MaximumLength = 250;

    /// <summary>Seeded system pages. Their slugs are immutable and identify a canonical short route.</summary>
    public static class System
    {
        public const string About = "about";
        public const string Terms = "terms";
        public const string Privacy = "privacy";
        public const string Contact = "contact";

        public static readonly string[] All = [About, Terms, Privacy, Contact];
    }

    /// <summary>
    /// First path segments already owned by the application. Taken from the actual route table
    /// (storefront, customer, admin, auth, API and SEO endpoints) plus the system page slugs.
    /// </summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "customer", "api", "login", "register", "logout", "forgot-password",
        "reset-password", "access-denied", "error", "cart", "checkout", "payment",
        "product", "products", "shop", "search", "category", "categories", "brand",
        "blog", "faq", "page", "about", "contact", "terms", "privacy",
        "sitemap.xml", "robots.txt", "sitemaps", "_blazor", "_framework", "uploads", "media"
    };

    public static bool IsSystemSlug(string? slug) =>
        slug is not null && System.All.Contains(slug.Trim(), StringComparer.OrdinalIgnoreCase);

    public static bool IsReserved(string? slug) =>
        slug is not null && Reserved.Contains(slug.Trim());

    /// <summary>
    /// Trim + lower-case, matching the existing product-tag normalisation convention. Persian
    /// characters are preserved: the storefront route accepts them and they are URL-escaped by the
    /// caller, so a Persian slug remains usable.
    /// </summary>
    public static string Normalize(string? slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>
    /// Normalises and validates a slug supplied by an administrator for a custom page.
    /// </summary>
    public static string NormalizeForCustomPage(string? slug)
    {
        var normalized = Normalize(slug);

        if (string.IsNullOrWhiteSpace(normalized))
            throw new BusinessException("نشانی صفحه (Slug) الزامی است.");
        if (normalized.Length > MaximumLength)
            throw new BusinessException($"نشانی صفحه نمی‌تواند بیشتر از {MaximumLength} نویسه باشد.");
        if (normalized.Any(char.IsWhiteSpace) || normalized.Contains('/') || normalized.Contains('\\') ||
            normalized.Contains('?') || normalized.Contains('#'))
            throw new BusinessException("نشانی صفحه نمی‌تواند شامل فاصله یا نویسه‌های / \\ ? # باشد.");
        if (IsReserved(normalized))
            throw new BusinessException($"نشانی «{normalized}» رزرو شده است و برای صفحه سفارشی قابل استفاده نیست.");

        return normalized;
    }
}
