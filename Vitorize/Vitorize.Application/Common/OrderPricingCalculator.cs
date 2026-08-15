using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// The single authoritative money decomposition for an order or an order preview.
/// Cart preview, coupon preview and checkout all consume this; no layer re-implements VAT arithmetic.
/// </summary>
/// <param name="DiscountedProductAmount">
/// Product money after discount and before VAT. The existing "free orders are unsupported"
/// invariant is evaluated on this value, never on <see cref="FinalAmount"/>, so VAT can never
/// turn a fully discounted basket into a payable tax-only order.
/// </param>
public sealed record OrderPricing(
    decimal SubtotalAmount,
    decimal DiscountAmount,
    bool VatEnabled,
    decimal VatRatePercent,
    VatCalculationMode VatCalculationMode,
    decimal VatTaxableAmount,
    decimal VatAmount,
    decimal FinalAmount,
    decimal DiscountedProductAmount)
{
    /// <summary>True when the basket carries no payable product value, with or without VAT.</summary>
    public bool IsZeroPayable => DiscountedProductAmount <= 0m;
}

public static class OrderPricingCalculator
{
    /// <summary>Money scale used by every persisted decimal(18,2) amount.</summary>
    public const int MoneyScale = 2;

    /// <summary>
    /// FIX-13 establishes the explicit money rounding convention. Every amount the calculator
    /// returns is rounded here, so the persisted reconciliation identity holds exactly instead of
    /// depending on SQL Server's implicit scale conversion.
    /// </summary>
    public static decimal RoundMoney(decimal value) =>
        Math.Round(value, MoneyScale, MidpointRounding.AwayFromZero);

    public static OrderPricing Calculate(decimal subtotal, decimal discountAmount, VatSettingsSnapshot vat)
    {
        ArgumentNullException.ThrowIfNull(vat);

        // Defensive normalisation mirroring the existing coupon contract: a discount is never
        // negative and never exceeds the subtotal (CouponService already caps it).
        var normalizedSubtotal = RoundMoney(Math.Max(0m, subtotal));
        var normalizedDiscount = RoundMoney(Math.Clamp(discountAmount, 0m, normalizedSubtotal));
        var discountedProductAmount = RoundMoney(normalizedSubtotal - normalizedDiscount);

        if (!vat.Enabled)
        {
            // Byte-identical to the pre-FIX-13 behaviour: final payable is subtotal minus discount.
            return new OrderPricing(
                normalizedSubtotal, normalizedDiscount, false, 0m, VatCalculationMode.BeforeDiscount,
                0m, 0m, discountedProductAmount, discountedProductAmount);
        }

        var ratePercent = Math.Clamp(vat.RatePercent, VatSettings.MinimumRatePercent, VatSettings.MaximumRatePercent);
        var taxableAmount = vat.CalculationMode == VatCalculationMode.AfterDiscount
            ? discountedProductAmount
            : normalizedSubtotal;
        taxableAmount = RoundMoney(Math.Max(0m, taxableAmount));

        var vatAmount = RoundMoney(taxableAmount * ratePercent / 100m);

        // Composed from already-rounded parts so the persisted identity is exact:
        //   BeforeDiscount : Subtotal + Vat - Discount == Final
        //   AfterDiscount  : VatTaxableAmount + Vat    == Final
        var finalAmount = vat.CalculationMode == VatCalculationMode.AfterDiscount
            ? taxableAmount + vatAmount
            : normalizedSubtotal + vatAmount - normalizedDiscount;
        finalAmount = RoundMoney(Math.Max(0m, finalAmount));

        return new OrderPricing(
            normalizedSubtotal, normalizedDiscount, true, ratePercent, vat.CalculationMode,
            taxableAmount, vatAmount, finalAmount, discountedProductAmount);
    }
}
