using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The inventory invariant: Instant delivery is gift-code driven, every other delivery mode uses the
/// administrator-managed per-variant quantity, and the two regimes never read each other's numbers.
///
/// These are the tests that replaced the hand-run SQL probe used while the feature was being built.
/// </summary>
public sealed class ProductAvailabilityRulesTests
{
    private const byte Instant = (byte)DeliveryType.Instant;
    private const byte Manual = (byte)DeliveryType.Manual;
    private const byte SupportRequired = (byte)DeliveryType.SupportRequired;

    // ---------------------------------------------------------------- regime selection

    [Fact]
    public void Only_instant_is_gift_code_driven()
    {
        Assert.True(ProductAvailabilityRules.IsGiftCodeDriven(Instant));
        Assert.False(ProductAvailabilityRules.IsGiftCodeDriven(Manual));
        Assert.False(ProductAvailabilityRules.IsGiftCodeDriven(SupportRequired));
    }

    [Fact]
    public void Every_non_instant_mode_uses_managed_stock()
    {
        Assert.False(ProductAvailabilityRules.IsManagedStock(Instant));
        Assert.True(ProductAvailabilityRules.IsManagedStock(Manual));
        Assert.True(ProductAvailabilityRules.IsManagedStock(SupportRequired));
    }

    [Fact]
    public void An_unknown_future_delivery_mode_defaults_to_managed_stock_not_unlimited()
    {
        // The bug this guards against: Manual used to fall through to a hard-coded 999999. A new mode
        // must inherit the safe regime, never the unlimited one.
        const byte hypotheticalNewMode = 99;
        Assert.True(ProductAvailabilityRules.IsManagedStock(hypotheticalNewMode));
        Assert.Equal(ProductVariantStockMode.Manual, ProductAvailabilityRules.RequiredStockMode(hypotheticalNewMode));
    }

    [Fact]
    public void Required_stock_mode_follows_delivery_type()
    {
        Assert.Equal(ProductVariantStockMode.GiftCode, ProductAvailabilityRules.RequiredStockMode(Instant));
        Assert.Equal(ProductVariantStockMode.Manual, ProductAvailabilityRules.RequiredStockMode(Manual));
        Assert.Equal(ProductVariantStockMode.Manual, ProductAvailabilityRules.RequiredStockMode(SupportRequired));
    }

    // ---------------------------------------------------------------- managed stock

    [Theory]
    [InlineData(Manual, 10, true)]
    [InlineData(Manual, 1, true)]
    [InlineData(Manual, 0, false)]
    [InlineData(SupportRequired, 10, true)]
    [InlineData(SupportRequired, 1, true)]
    [InlineData(SupportRequired, 0, false)]
    public void Managed_stock_decides_availability_for_non_instant(byte deliveryType, int stock, bool expected)
    {
        // availableGiftCodes is deliberately 0: it must not influence a managed-stock SKU.
        Assert.Equal(expected, ProductAvailabilityRules.IsInStock(deliveryType, availableGiftCodes: 0, stockQuantity: stock));
        Assert.Equal(stock, ProductAvailabilityRules.AvailableUnits(deliveryType, 0, stock));
    }

    [Fact]
    public void Restocking_from_zero_restores_availability()
    {
        Assert.False(ProductAvailabilityRules.IsInStock(Manual, 0, 0));
        Assert.True(ProductAvailabilityRules.IsInStock(Manual, 0, 5));
    }

    [Fact]
    public void Support_required_is_available_on_managed_stock_without_any_gift_codes()
    {
        // The regression: SupportRequired previously fell into the gift-code branch and read 0, so it
        // showed as ناموجود no matter how much real stock existed.
        Assert.True(ProductAvailabilityRules.IsInStock(SupportRequired, availableGiftCodes: 0, stockQuantity: 4));
    }

    // ---------------------------------------------------------------- instant

    [Theory]
    [InlineData(5, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    public void Instant_availability_is_the_gift_code_count(int codes, bool expected)
    {
        Assert.Equal(expected, ProductAvailabilityRules.IsInStock(Instant, codes, stockQuantity: 0));
        Assert.Equal(codes, ProductAvailabilityRules.AvailableUnits(Instant, codes, 0));
    }

    [Fact]
    public void A_dormant_stock_quantity_can_never_make_an_instant_variant_sellable()
    {
        // This is what makes preserving StockQuantity on an Instant variant safe rather than
        // destructive: the value is inert while the product is Instant.
        Assert.False(ProductAvailabilityRules.IsInStock(Instant, availableGiftCodes: 0, stockQuantity: 100));
        Assert.Equal(0, ProductAvailabilityRules.AvailableUnits(Instant, 0, 100));
    }

    [Fact]
    public void Managed_stock_is_ignored_for_instant_even_when_codes_exist()
    {
        Assert.Equal(3, ProductAvailabilityRules.AvailableUnits(Instant, availableGiftCodes: 3, stockQuantity: 999));
    }

    // ---------------------------------------------------------------- quantity satisfaction

    [Theory]
    [InlineData(5, 1, true)]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    [InlineData(0, 1, false)]
    [InlineData(5, 0, false)]      // zero is not a purchasable quantity
    [InlineData(5, -1, false)]
    public void Can_satisfy_respects_managed_stock(int stock, int requested, bool expected) =>
        Assert.Equal(expected, ProductAvailabilityRules.CanSatisfy(Manual, 0, stock, requested));

    [Theory]
    [InlineData(5, 5, true)]
    [InlineData(5, 6, false)]
    public void Can_satisfy_respects_gift_code_stock_for_instant(int codes, int requested, bool expected) =>
        Assert.Equal(expected, ProductAvailabilityRules.CanSatisfy(Instant, codes, stockQuantity: 0, requested));

    // ---------------------------------------------------------------- defensive clamping

    [Fact]
    public void Negative_persisted_values_never_surface_as_negative_availability()
    {
        // The database CHECK prevents this, but availability must degrade to "unavailable" rather than
        // propagate a negative if a value ever slipped through.
        Assert.Equal(0, ProductAvailabilityRules.AvailableUnits(Manual, 0, -5));
        Assert.False(ProductAvailabilityRules.IsInStock(Manual, 0, -5));
        Assert.Equal(0, ProductAvailabilityRules.AvailableUnits(Instant, -5, 0));
    }
}
