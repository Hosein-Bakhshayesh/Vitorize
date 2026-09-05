namespace Vitorize.Application.Common;

/// <summary>Central names and safe defaults for storefront product-review moderation.</summary>
public static class ProductReviewSettings
{
    public const string GroupName = "Reviews";
    public const string AutoApproveKey = "Reviews.AutoApprove";

    // Existing installations behaved as auto-approval before this setting existed.
    public static bool IsAutoApproveEnabled(string? value) =>
        !bool.TryParse(value, out var enabled) || enabled;
}
