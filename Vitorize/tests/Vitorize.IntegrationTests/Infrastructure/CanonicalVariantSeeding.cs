using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests.Infrastructure;

/// <summary>
/// Inventory, cart validation, checkout revalidation and paid-time consumption are all SKU-scoped,
/// so every purchasable non-Instant product owns at least one active ProductVariant. Production
/// guarantees this from both ends: AdminProductService creates the implicit default SKU on
/// create/update, and V0021 backfills existing rows.
///
/// Tests that seed products straight through EF bypass both, so they must uphold the invariant
/// themselves — otherwise they assert against a shape the application can no longer produce.
/// </summary>
internal static class CanonicalVariantSeeding
{
    /// <summary>
    /// Attaches the canonical SKU a non-Instant product would have in production. Stock defaults
    /// far above anything the suites order, so the subject of a test stays whatever it was about
    /// rather than becoming the stock ceiling. Instant products are returned untouched: their
    /// availability is the gift-code pool.
    /// </summary>
    public static Product WithCanonicalVariant(this Product product, int stockQuantity = 1000)
    {
        if (ProductAvailabilityRules.IsGiftCodeDriven(product.DeliveryType))
            return product;

        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Title = "پیش‌فرض",
            Price = product.BasePrice,
            DiscountPrice = product.DiscountPrice,
            StockMode = (byte)ProductAvailabilityRules.RequiredStockMode(product.DeliveryType),
            StockQuantity = stockQuantity,
            IsDefault = true,
            IsActive = true,
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        });
        return product;
    }
}
