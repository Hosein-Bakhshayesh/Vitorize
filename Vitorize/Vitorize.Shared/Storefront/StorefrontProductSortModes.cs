namespace Vitorize.Shared.Storefront;

/// <summary>
/// The storefront's default product ordering, chosen by an administrator and applied whenever a
/// customer has not asked for a specific order.
///
/// Two different things are deliberately kept apart here. The stable <see cref="Codes"/> are what an
/// administrator picks and what is persisted; the <see cref="QueryKeys"/> are the sort keys the
/// public product query already understands and that appear in customer URLs as <c>?sort=</c>. A
/// customer's explicit choice is always a query key, so it can never be confused with the saved
/// default, and only the query keys listed here are honoured.
///
/// Modes are listed only where the product query has real semantics for them. There is deliberately
/// no "most popular" mode: Vitorize stores no popularity signal - no view count, no purchase count,
/// no ranking score - and the home page's "popular" row is simply the default listing under a
/// heading. Offering it here would rank products by nothing at all. Best-selling is offered instead,
/// because that one is real: it is the same paid-order quantity the admin dashboard already reports.
/// </summary>
public static class StorefrontProductSortModes
{
    /// <summary>Settings key holding the administrator's choice.</summary>
    public const string SettingKey = "StorefrontDefaultProductSort";

    /// <summary>
    /// Applied when nothing is saved. A brand-new install and an existing production database that
    /// has never seen this key both behave the same way, and neither can produce an arbitrary order.
    /// </summary>
    public const string Default = "AvailabilityFirst";

    /// <summary>Stable code -> the query key the public product listing already understands.</summary>
    private static readonly IReadOnlyDictionary<string, string> Modes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AvailabilityFirst"] = "availability",
            ["BestSelling"] = "bestselling",
            ["Newest"] = "newest",
            ["Oldest"] = "oldest",
            ["PriceLowToHigh"] = "cheapest",
            ["PriceHighToLow"] = "expensive",
            ["MostDiscounted"] = "discount"
        };

    /// <summary>Persian labels, for the admin control and the customer's sort menu.</summary>
    private static readonly IReadOnlyDictionary<string, string> Labels =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["AvailabilityFirst"] = "موجودها اول",
            ["BestSelling"] = "پرفروش‌ترین",
            ["Newest"] = "جدیدترین",
            ["Oldest"] = "قدیمی‌ترین",
            ["PriceLowToHigh"] = "ارزان‌ترین",
            ["PriceHighToLow"] = "گران‌ترین",
            ["MostDiscounted"] = "بیشترین تخفیف"
        };

    public static IEnumerable<KeyValuePair<string, string>> All =>
        Modes.Keys.Select(code => new KeyValuePair<string, string>(code, Labels[code]));

    public static bool IsSupported(string? code) => !string.IsNullOrWhiteSpace(code) && Modes.ContainsKey(code.Trim());

    /// <summary>A stored value that is missing, blank or no longer supported resolves to the default.</summary>
    public static string Normalize(string? code) =>
        IsSupported(code)
            ? Modes.Keys.First(x => string.Equals(x, code!.Trim(), StringComparison.OrdinalIgnoreCase))
            : Default;

    /// <summary>The query key the product listing should use for a saved code.</summary>
    public static string ToQueryKey(string? code) => Modes[Normalize(code)];

    /// <summary>The saved code a customer's explicit query key corresponds to, when one does.</summary>
    public static string? FromQueryKey(string? queryKey) =>
        string.IsNullOrWhiteSpace(queryKey)
            ? null
            : Modes.FirstOrDefault(x => string.Equals(x.Value, queryKey.Trim(), StringComparison.OrdinalIgnoreCase)).Key;

    public static string Label(string? code) => Labels[Normalize(code)];
}
