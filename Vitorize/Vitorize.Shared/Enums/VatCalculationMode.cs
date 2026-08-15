namespace Vitorize.Shared.Enums
{
    /// <summary>
    /// Whether VAT is calculated on the order subtotal before the coupon discount is applied,
    /// or on the discounted amount. Snapshotted on each order at creation time.
    /// </summary>
    public enum VatCalculationMode : byte
    {
        BeforeDiscount = 1,
        AfterDiscount = 2
    }
}
