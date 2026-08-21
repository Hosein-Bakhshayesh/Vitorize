using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The availability truth table. Badges, listings, cart, checkout and payment all answer
/// <see cref="ProductAvailabilityRules"/>, so this is the one place the precedence is pinned:
/// the administrator's manual override outranks every inventory consideration, Unlimited outranks
/// counted quantity, and Instant delivery never leaves the gift-code regime.
/// </summary>
public sealed class ProductEffectiveAvailabilityTests
{
    private const byte Instant = (byte)DeliveryType.Instant;
    private const byte Manual = (byte)DeliveryType.Manual;

    [Fact]
    public void Forced_out_of_stock_beats_unlimited()
    {
        Assert.False(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: true, Manual, ProductVariantStockMode.Unlimited,
            availableGiftCodes: 0, stockQuantity: 0));
        Assert.False(ProductAvailabilityRules.CanSell(
            forceOutOfStock: true, Manual, ProductVariantStockMode.Unlimited, 0, 0, requested: 1));
    }

    [Fact]
    public void Forced_out_of_stock_beats_a_large_counted_quantity()
    {
        Assert.False(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: true, Manual, ProductVariantStockMode.Manual,
            availableGiftCodes: 0, stockQuantity: 100));
        Assert.False(ProductAvailabilityRules.CanSell(
            forceOutOfStock: true, Manual, ProductVariantStockMode.Manual, 0, 100, requested: 1));
    }

    [Fact]
    public void Forced_out_of_stock_beats_an_eligible_gift_code_pool()
    {
        Assert.False(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: true, Instant, ProductVariantStockMode.GiftCode,
            availableGiftCodes: 25, stockQuantity: 0));
    }

    [Fact]
    public void Unlimited_is_available_and_sells_any_quantity()
    {
        Assert.True(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Unlimited,
            availableGiftCodes: 0, stockQuantity: 0));

        // The point of the policy: a zero counted quantity is irrelevant, and no sentinel number
        // is needed to express "as many as the customer wants".
        Assert.True(ProductAvailabilityRules.CanSell(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Unlimited, 0, 0, requested: 1));
        Assert.True(ProductAvailabilityRules.CanSell(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Unlimited, 0, 0, requested: 5_000));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Managed_stock_decides_when_no_override_applies(int stockQuantity, bool expected)
    {
        Assert.Equal(expected, ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Manual,
            availableGiftCodes: 0, stockQuantity: stockQuantity));
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Instant_availability_follows_the_gift_code_pool(int availableGiftCodes, bool expected)
    {
        Assert.Equal(expected, ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: false, Instant, ProductVariantStockMode.GiftCode,
            availableGiftCodes: availableGiftCodes, stockQuantity: 0));
    }

    [Fact]
    public void Managed_stock_cannot_oversell_and_a_gift_code_pool_cannot_borrow_stock()
    {
        Assert.False(ProductAvailabilityRules.CanSell(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Manual, 0, stockQuantity: 2, requested: 3));
        Assert.True(ProductAvailabilityRules.CanSell(
            forceOutOfStock: false, Manual, ProductVariantStockMode.Manual, 0, stockQuantity: 2, requested: 2));

        // An Instant SKU must never draw on a counted quantity, however large.
        Assert.False(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: false, Instant, ProductVariantStockMode.GiftCode,
            availableGiftCodes: 0, stockQuantity: 999));
    }

    [Fact]
    public void A_non_positive_quantity_is_never_sellable()
    {
        Assert.False(ProductAvailabilityRules.CanSell(
            false, Manual, ProductVariantStockMode.Unlimited, 0, 0, requested: 0));
        Assert.False(ProductAvailabilityRules.CanSell(
            false, Manual, ProductVariantStockMode.Manual, 0, 10, requested: -1));
    }

    [Fact]
    public void Instant_delivery_can_never_be_declared_unlimited()
    {
        // Unlimited gift codes would promise codes the fulfilment pipeline cannot produce.
        Assert.Equal(ProductVariantStockMode.GiftCode,
            ProductAvailabilityRules.NormalizeStockMode(Instant, ProductVariantStockMode.Unlimited));

        // Even if a legacy row already claimed it, availability stays gift-code driven.
        Assert.False(ProductAvailabilityRules.IsAvailableForSale(
            forceOutOfStock: false, Instant, ProductVariantStockMode.Unlimited,
            availableGiftCodes: 0, stockQuantity: 0));
    }

    [Fact]
    public void Managed_delivery_keeps_a_requested_unlimited_policy()
    {
        Assert.Equal(ProductVariantStockMode.Unlimited,
            ProductAvailabilityRules.NormalizeStockMode(Manual, ProductVariantStockMode.Unlimited));
        Assert.Equal(ProductVariantStockMode.Manual,
            ProductAvailabilityRules.NormalizeStockMode(Manual, ProductVariantStockMode.Manual));
        // A gift-code mode requested for managed delivery is not a valid choice there.
        Assert.Equal(ProductVariantStockMode.Manual,
            ProductAvailabilityRules.NormalizeStockMode(Manual, ProductVariantStockMode.GiftCode));
    }

    [Fact]
    public void Only_counted_managed_stock_is_consumed_by_a_payment()
    {
        Assert.True(ProductAvailabilityRules.ConsumesStockOnPayment(Manual, ProductVariantStockMode.Manual));
        // Unlimited must never be decremented - that is what makes it unlimited.
        Assert.False(ProductAvailabilityRules.ConsumesStockOnPayment(Manual, ProductVariantStockMode.Unlimited));
        // Instant consumes gift codes, not a quantity.
        Assert.False(ProductAvailabilityRules.ConsumesStockOnPayment(Instant, ProductVariantStockMode.GiftCode));
    }
}
