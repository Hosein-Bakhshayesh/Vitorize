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
}
