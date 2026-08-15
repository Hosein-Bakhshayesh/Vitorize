using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-13 (Client Issue #11). The single authoritative VAT decomposition. Cart preview, coupon
/// preview and checkout all route through <see cref="OrderPricingCalculator"/>, so these tests pin
/// the money contract for every surface at once.
/// </summary>
public sealed class Fix13VatCalculatorTests
{
    private static VatSettingsSnapshot Before(decimal rate) => new(true, rate, VatCalculationMode.BeforeDiscount);
    private static VatSettingsSnapshot After(decimal rate) => new(true, rate, VatCalculationMode.AfterDiscount);

    [Fact]
    public void Vat_disabled_reproduces_the_pre_fix13_behaviour()
    {
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 100_000m, VatSettingsSnapshot.Disabled);

        pricing.VatEnabled.Should().BeFalse();
        pricing.VatRatePercent.Should().Be(0m);
        pricing.VatTaxableAmount.Should().Be(0m);
        pricing.VatAmount.Should().Be(0m);
        pricing.FinalAmount.Should().Be(900_000m, "final payable stays subtotal minus discount");
    }

    [Fact]
    public void Zero_rate_with_vat_enabled_adds_no_tax()
    {
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 0m, Before(0m));

        pricing.VatEnabled.Should().BeTrue();
        pricing.VatAmount.Should().Be(0m);
        pricing.FinalAmount.Should().Be(1_000_000m);
    }

    [Fact]
    public void Before_discount_without_a_coupon_taxes_the_whole_subtotal()
    {
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 0m, Before(10m));

        pricing.VatTaxableAmount.Should().Be(1_000_000m);
        pricing.VatAmount.Should().Be(100_000m);
        pricing.FinalAmount.Should().Be(1_100_000m);
    }

    [Fact]
    public void Before_discount_taxes_the_full_subtotal_even_with_a_percentage_coupon()
    {
        // The approved worked example: 1,000,000 subtotal, 10% VAT, 100,000 coupon -> 1,000,000.
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 100_000m, Before(10m));

        pricing.VatTaxableAmount.Should().Be(1_000_000m);
        pricing.VatAmount.Should().Be(100_000m);
        pricing.FinalAmount.Should().Be(1_000_000m);
    }

    [Fact]
    public void Before_discount_taxes_the_full_subtotal_with_a_fixed_coupon()
    {
        var pricing = OrderPricingCalculator.Calculate(500_000m, 120_000m, Before(9m));

        pricing.VatTaxableAmount.Should().Be(500_000m);
        pricing.VatAmount.Should().Be(45_000m);
        pricing.FinalAmount.Should().Be(425_000m);
    }

    [Fact]
    public void After_discount_without_a_coupon_matches_before_discount()
    {
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 0m, After(10m));

        pricing.VatTaxableAmount.Should().Be(1_000_000m);
        pricing.VatAmount.Should().Be(100_000m);
        pricing.FinalAmount.Should().Be(1_100_000m);
    }

    [Fact]
    public void After_discount_taxes_only_the_discounted_amount_with_a_percentage_coupon()
    {
        // The approved worked example: 1,000,000 subtotal, 100,000 coupon, 10% VAT -> 990,000.
        var pricing = OrderPricingCalculator.Calculate(1_000_000m, 100_000m, After(10m));

        pricing.VatTaxableAmount.Should().Be(900_000m);
        pricing.VatAmount.Should().Be(90_000m);
        pricing.FinalAmount.Should().Be(990_000m);
    }

    [Fact]
    public void After_discount_taxes_only_the_discounted_amount_with_a_fixed_coupon()
    {
        var pricing = OrderPricingCalculator.Calculate(500_000m, 120_000m, After(9m));

        pricing.VatTaxableAmount.Should().Be(380_000m);
        pricing.VatAmount.Should().Be(34_200m);
        pricing.FinalAmount.Should().Be(414_200m);
    }

    [Theory]
    // 12.5% of 1,000.05 = 125.00625 -> 125.01 away from zero.
    [InlineData(1000.05, 12.5, 125.01)]
    // Exact .005 midpoint must round up, never to even.
    [InlineData(100.10, 5, 5.01)]
    [InlineData(0.10, 5, 0.01)]
    public void Vat_rounds_to_two_decimals_away_from_zero(decimal subtotal, decimal rate, decimal expectedVat)
    {
        var pricing = OrderPricingCalculator.Calculate(subtotal, 0m, Before(rate));

        pricing.VatAmount.Should().Be(expectedVat);
    }

    [Theory]
    [InlineData(1000.05, 333.33, 12.5)]
    [InlineData(999.99, 0.01, 9)]
    [InlineData(12345.67, 1234.56, 7.35)]
    public void Reconciliation_identity_holds_exactly_in_both_modes(decimal subtotal, decimal discount, decimal rate)
    {
        var before = OrderPricingCalculator.Calculate(subtotal, discount, Before(rate));
        (before.SubtotalAmount + before.VatAmount - before.DiscountAmount)
            .Should().Be(before.FinalAmount, "BeforeDiscount identity must hold on persisted values");

        var after = OrderPricingCalculator.Calculate(subtotal, discount, After(rate));
        (after.VatTaxableAmount + after.VatAmount)
            .Should().Be(after.FinalAmount, "AfterDiscount identity must hold on persisted values");
    }

    [Theory]
    [InlineData(1000.05, 333.33, 12.5)]
    [InlineData(12345.67, 1234.56, 7.35)]
    public void Every_returned_amount_is_scaled_to_two_decimals(decimal subtotal, decimal discount, decimal rate)
    {
        foreach (var pricing in new[]
                 {
                     OrderPricingCalculator.Calculate(subtotal, discount, Before(rate)),
                     OrderPricingCalculator.Calculate(subtotal, discount, After(rate))
                 })
        {
            foreach (var amount in new[]
                     {
                         pricing.SubtotalAmount, pricing.DiscountAmount, pricing.VatTaxableAmount,
                         pricing.VatAmount, pricing.FinalAmount, pricing.DiscountedProductAmount
                     })
            {
                decimal.Round(amount, 2).Should().Be(amount, "decimal(18,2) must never round a persisted amount");
            }
        }
    }

    [Fact]
    public void A_full_discount_is_zero_payable_in_both_modes_and_never_becomes_tax_only()
    {
        var before = OrderPricingCalculator.Calculate(1_000_000m, 1_000_000m, Before(10m));
        var after = OrderPricingCalculator.Calculate(1_000_000m, 1_000_000m, After(10m));

        before.IsZeroPayable.Should().BeTrue("the guard reads the product amount before VAT");
        after.IsZeroPayable.Should().BeTrue();
        after.VatAmount.Should().Be(0m);
        // BeforeDiscount would otherwise produce a positive tax-only payable; the caller must reject it.
        before.FinalAmount.Should().Be(100_000m);
    }

    [Fact]
    public void Defensive_boundaries_clamp_unsafe_inputs()
    {
        OrderPricingCalculator.Calculate(-50m, 10m, Before(10m)).SubtotalAmount.Should().Be(0m);
        OrderPricingCalculator.Calculate(100m, -10m, Before(10m)).DiscountAmount.Should().Be(0m);
        OrderPricingCalculator.Calculate(100m, 500m, Before(10m)).DiscountAmount
            .Should().Be(100m, "a discount never exceeds the subtotal");
        OrderPricingCalculator.Calculate(100m, 0m, new VatSettingsSnapshot(true, 500m, VatCalculationMode.BeforeDiscount))
            .VatRatePercent.Should().Be(100m, "an out-of-range rate is clamped, never applied raw");
        OrderPricingCalculator.Calculate(100m, 0m, new VatSettingsSnapshot(true, -5m, VatCalculationMode.BeforeDiscount))
            .VatRatePercent.Should().Be(0m);
    }

    [Fact]
    public void Fractional_line_totals_and_quantities_stay_consistent()
    {
        // Three lines of 33.33 x 3 = 299.97 subtotal.
        var subtotal = 3 * (33.33m * 3);
        var pricing = OrderPricingCalculator.Calculate(subtotal, 0m, After(9m));

        pricing.SubtotalAmount.Should().Be(299.97m);
        pricing.VatTaxableAmount.Should().Be(299.97m);
        pricing.VatAmount.Should().Be(27.00m);
        pricing.FinalAmount.Should().Be(326.97m);
    }
}
