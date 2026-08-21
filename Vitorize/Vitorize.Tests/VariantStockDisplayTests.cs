using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// What a SKU's inventory reads as on an administrative surface.
///
/// The reported defect: an administrator saved a stock of 17 on a manually counted SKU, the storefront
/// and the edit modal showed 17, and the variant tables showed 0 — because those tables rendered the
/// gift-code pool count, which is always zero for a SKU that has no gift codes.
/// </summary>
public sealed class VariantStockDisplayTests
{
    private static ProductAvailabilityRules.VariantStockDisplay Describe(
        DeliveryType delivery, ProductVariantStockMode mode, int quantity, int giftCodes) =>
        ProductAvailabilityRules.DescribeVariantStock((byte)delivery, (byte)mode, quantity, giftCodes);

    [Fact]
    public void A_manually_counted_sku_shows_its_persisted_quantity_not_the_gift_code_pool()
    {
        var display = Describe(DeliveryType.Manual, ProductVariantStockMode.Manual, quantity: 17, giftCodes: 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.Counted, display.Kind);
        Assert.Equal(17, display.Units);
    }

    [Fact]
    public void A_support_required_sku_is_counted_too()
    {
        var display = Describe(DeliveryType.SupportRequired, ProductVariantStockMode.Manual, 42, 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.Counted, display.Kind);
        Assert.Equal(42, display.Units);
    }

    [Fact]
    public void An_unlimited_sku_is_not_reported_as_a_quantity_at_all()
    {
        // Printing 0 here was the misleading case called out in the requirement: unlimited stock must
        // never read as unavailable.
        var display = Describe(DeliveryType.Manual, ProductVariantStockMode.Unlimited, quantity: 0, giftCodes: 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.Unlimited, display.Kind);
    }

    [Fact]
    public void An_unlimited_sku_keeps_its_dormant_quantity_out_of_the_display()
    {
        // The number is preserved in storage so switching back restores it, but it is not shown as
        // sellable inventory while the SKU is unlimited.
        var display = Describe(DeliveryType.Manual, ProductVariantStockMode.Unlimited, quantity: 250, giftCodes: 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.Unlimited, display.Kind);
    }

    [Fact]
    public void An_instant_sku_reads_from_the_gift_code_pool()
    {
        var display = Describe(DeliveryType.Instant, ProductVariantStockMode.GiftCode, quantity: 0, giftCodes: 9);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.GiftCodePool, display.Kind);
        Assert.Equal(9, display.Units);
    }

    [Fact]
    public void An_instant_sku_never_reports_a_dormant_counted_quantity_as_stock()
    {
        // Rows written outside the admin service can carry an un-normalised Manual mode and a leftover
        // quantity. The delivery type decides the regime, so those units are not claimed as sellable.
        var display = Describe(DeliveryType.Instant, ProductVariantStockMode.Manual, quantity: 250, giftCodes: 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.GiftCodePool, display.Kind);
        Assert.Equal(0, display.Units);
    }

    [Fact]
    public void A_negative_quantity_never_reaches_the_display()
    {
        Assert.Equal(0, Describe(DeliveryType.Manual, ProductVariantStockMode.Manual, -5, 0).Units);
        Assert.Equal(0, Describe(DeliveryType.Instant, ProductVariantStockMode.GiftCode, 0, -5).Units);
    }

    [Fact]
    public void An_unrecognised_stock_mode_on_a_managed_product_still_shows_the_real_number()
    {
        var display = Describe(DeliveryType.Manual, (ProductVariantStockMode)99, quantity: 7, giftCodes: 0);

        Assert.Equal(ProductAvailabilityRules.VariantStockDisplayKind.Counted, display.Kind);
        Assert.Equal(7, display.Units);
    }

    [Fact]
    public void The_display_agrees_with_the_availability_rule_it_mirrors()
    {
        // Same inputs, same answer as the rule checkout uses — the display can never claim units the
        // shop cannot sell.
        foreach (var delivery in new[] { DeliveryType.Instant, DeliveryType.Manual, DeliveryType.SupportRequired })
        {
            var display = Describe(delivery, ProductVariantStockMode.Manual, quantity: 17, giftCodes: 4);
            if (display.Kind == ProductAvailabilityRules.VariantStockDisplayKind.Unlimited) continue;

            Assert.Equal(
                ProductAvailabilityRules.AvailableUnits((byte)delivery, availableGiftCodes: 4, stockQuantity: 17),
                display.Units);
        }
    }
}
