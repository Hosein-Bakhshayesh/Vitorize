namespace Vitorize.Application.DTOs.Coupons
{
    public class ValidateCouponResultDto
    {
        public Guid CouponId { get; set; }
        public string Code { get; set; } = string.Empty;
        public decimal OrderAmount { get; set; }
        public decimal DiscountAmount { get; set; }

        // Server-computed VAT decomposition for the coupon preview. FinalAmount below is the
        // authoritative VAT-inclusive payable and must be rendered as-is by the storefront.
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal FinalAmount { get; set; }
    }
}