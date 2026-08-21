using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// The single definition of what "in stock" means for a Vitorize SKU.
///
/// Inventory lives on ProductVariant because the variant is the purchasable unit: cart items, order
/// items, gift codes, gift-code batches and gift-code reservations are all variant-scoped. Tracking
/// it on Product would let one variant consume another variant's inventory.
///
/// Two regimes, decided by the product's delivery type and never mixed:
///
///   Instant (DeliveryType.Instant)
///       Availability is the count of eligible, still-available gift codes. Administrators cannot
///       type a number that claims stock the fulfilment pipeline cannot actually deliver.
///
///   Non-Instant (DeliveryType.Manual, DeliveryType.SupportRequired)
///       Availability is the variant's managed StockQuantity, consumed only on authoritative
///       payment success.
///
/// Storefront projections express these rules inline because they must translate to SQL; this class
/// is the in-memory counterpart used by callers that already hold the values, and the place the
/// invariant is documented.
/// </summary>
public static class ProductAvailabilityRules
{
    /// <summary>
    /// Title carried by the implicit SKU created for a product the administrator regards as having
    /// no variants. AdminProductService creates it, V0021 backfills it, and the storefront uses it
    /// to tell an implicit SKU apart from a real one the administrator named and priced.
    /// </summary>
    public const string DefaultVariantTitle = "پیش‌فرض";

    /// <summary>
    /// True when a product's variant list represents a genuine choice for the customer. A single
    /// implicit SKU is not a choice, so the storefront must not render a one-item selector for it;
    /// a single SKU the administrator named and priced still carries meaning and stays visible.
    /// </summary>
    public static bool OffersVariantChoice(IReadOnlyCollection<string> variantTitles) =>
        variantTitles.Count > 1 ||
        (variantTitles.Count == 1 && !string.Equals(variantTitles.First(), DefaultVariantTitle, StringComparison.Ordinal));

    /// <summary>
    /// The variant name to show a customer, or null when there is nothing worth showing. The
    /// implicit SKU exists so inventory has somewhere to live; naming it on a cart line or an
    /// order would present internal plumbing as if the customer had chosen it.
    /// </summary>
    public static string? CustomerFacingVariantTitle(string? variantTitle) =>
        string.Equals(variantTitle, DefaultVariantTitle, StringComparison.Ordinal) ? null : variantTitle;

    /// <summary>True when the delivery type draws its inventory from gift codes.</summary>
    public static bool IsGiftCodeDriven(byte deliveryType) =>
        deliveryType == (byte)DeliveryType.Instant;

    /// <summary>
    /// True when the delivery type uses an administrator-managed quantity. Every purchasable
    /// non-Instant mode qualifies, so a new delivery mode defaults to managed stock rather than to
    /// the unlimited behaviour that previously let Manual products oversell.
    /// </summary>
    public static bool IsManagedStock(byte deliveryType) => !IsGiftCodeDriven(deliveryType);

    /// <summary>The stock mode a variant must carry, derived from its product's delivery type.</summary>
    public static ProductVariantStockMode RequiredStockMode(byte deliveryType) =>
        IsGiftCodeDriven(deliveryType) ? ProductVariantStockMode.GiftCode : ProductVariantStockMode.Manual;

    /// <summary>
    /// The stock mode a variant may keep once an administrator has chosen one.
    ///
    /// Delivery type still decides the regime - Instant is always gift-code driven and can never be
    /// declared unlimited, because its units are real codes that must exist before they can be
    /// delivered. Within managed delivery an administrator may legitimately choose between a counted
    /// quantity and Unlimited, so a requested Unlimited survives instead of being rewritten to
    /// Manual. Anything else falls back to the required mode.
    /// </summary>
    public static ProductVariantStockMode NormalizeStockMode(byte deliveryType, ProductVariantStockMode requested) =>
        IsGiftCodeDriven(deliveryType)
            ? ProductVariantStockMode.GiftCode
            : requested == ProductVariantStockMode.Unlimited
                ? ProductVariantStockMode.Unlimited
                : ProductVariantStockMode.Manual;

    /// <summary>
    /// True when the SKU carries no quantity limit. Unlimited is an inventory policy, never a large
    /// number: nothing in the system stores or shows a sentinel quantity for it.
    /// </summary>
    public static bool IsUnlimited(ProductVariantStockMode stockMode) =>
        stockMode == ProductVariantStockMode.Unlimited;

    /// <summary>True when a paid order must decrement this SKU's counted quantity.</summary>
    public static bool ConsumesStockOnPayment(byte deliveryType, ProductVariantStockMode stockMode) =>
        IsManagedStock(deliveryType) && !IsUnlimited(stockMode);

    /// <summary>
    /// Resolves available units for one variant.
    /// <paramref name="availableGiftCodes"/> is only consulted for Instant delivery, and
    /// <paramref name="stockQuantity"/> only for managed stock — neither leaks into the other.
    /// </summary>
    public static int AvailableUnits(byte deliveryType, int availableGiftCodes, int stockQuantity) =>
        IsGiftCodeDriven(deliveryType) ? Math.Max(0, availableGiftCodes) : Math.Max(0, stockQuantity);

    public static bool IsInStock(byte deliveryType, int availableGiftCodes, int stockQuantity) =>
        AvailableUnits(deliveryType, availableGiftCodes, stockQuantity) > 0;

    /// <summary>True when a requested quantity can be satisfied right now.</summary>
    public static bool CanSatisfy(byte deliveryType, int availableGiftCodes, int stockQuantity, int requested) =>
        requested > 0 && requested <= AvailableUnits(deliveryType, availableGiftCodes, stockQuantity);

    // ---------------------------------------------------------------- inventory display

    /// <summary>Which inventory a SKU's number actually comes from.</summary>
    public enum VariantStockDisplayKind : byte
    {
        /// <summary>A counted quantity an administrator maintains.</summary>
        Counted = 1,

        /// <summary>No quantity is tracked; the SKU never runs out.</summary>
        Unlimited = 2,

        /// <summary>Derived from the pool of undelivered gift codes, not from a counter.</summary>
        GiftCodePool = 3
    }

    /// <summary>The inventory to show for one SKU, and where the number came from.</summary>
    public readonly record struct VariantStockDisplay(VariantStockDisplayKind Kind, int Units);

    /// <summary>
    /// Resolves what a SKU's inventory should read as.
    ///
    /// Every administrative surface must go through this. The variant tables used to render the
    /// gift-code pool count for every row, which is always zero for a manually counted SKU — so an
    /// administrator who had just saved a stock of 17 was shown 0.
    ///
    /// The delivery type decides the inventory regime and the stock mode only refines it, in exactly
    /// the order <see cref="AvailableUnits"/> uses. Keying on the stock mode alone would be wrong for
    /// a gift-code product whose rows were written outside the admin service and so never had their
    /// mode normalised: it would print a dormant counted number as if those units were sellable.
    /// </summary>
    public static VariantStockDisplay DescribeVariantStock(
        byte deliveryType,
        byte stockMode,
        int stockQuantity,
        int availableGiftCodes)
    {
        if (IsGiftCodeDriven(deliveryType))
            return new(VariantStockDisplayKind.GiftCodePool, Math.Max(0, availableGiftCodes));

        if (IsUnlimited((ProductVariantStockMode)stockMode))
            return new(VariantStockDisplayKind.Unlimited, 0);

        // Manual, and any unrecognised mode on a managed product, are a counted quantity: showing
        // the real number is safer than implying an unlimited inventory.
        return new(VariantStockDisplayKind.Counted, Math.Max(0, stockQuantity));
    }

    // ---------------------------------------------------------------- effective availability
    //
    // Everything customer-facing and every purchase gate answers these two methods, so a badge can
    // never disagree with what the cart will accept. Precedence, highest first:
    //
    //   1. forceOutOfStock  - an administrator has taken the product off sale
    //   2. Unlimited        - no quantity limit applies
    //   3. Instant          - eligible gift codes decide
    //   4. managed stock    - the counted quantity decides

    /// <summary>
    /// Whether the SKU can be sold at all right now. <paramref name="forceOutOfStock"/> wins over
    /// every inventory consideration, including Unlimited.
    /// </summary>
    public static bool IsAvailableForSale(
        bool forceOutOfStock,
        byte deliveryType,
        ProductVariantStockMode stockMode,
        int availableGiftCodes,
        int stockQuantity)
    {
        if (forceOutOfStock) return false;
        if (IsUnlimited(stockMode) && IsManagedStock(deliveryType)) return true;
        return IsInStock(deliveryType, availableGiftCodes, stockQuantity);
    }

    /// <summary>
    /// Whether a specific quantity can be bought right now. An unlimited SKU accepts any positive
    /// quantity; everything else defers to the units it actually has.
    /// </summary>
    public static bool CanSell(
        bool forceOutOfStock,
        byte deliveryType,
        ProductVariantStockMode stockMode,
        int availableGiftCodes,
        int stockQuantity,
        int requested)
    {
        if (requested <= 0) return false;
        if (forceOutOfStock) return false;
        if (IsUnlimited(stockMode) && IsManagedStock(deliveryType)) return true;
        return CanSatisfy(deliveryType, availableGiftCodes, stockQuantity, requested);
    }
}
