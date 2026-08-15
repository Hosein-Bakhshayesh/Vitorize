namespace Vitorize.Application.DTOs.Cart
{
    public class CartDto
    {
        public Guid Id { get; set; }

        public Guid? UserId { get; set; }

        public List<CartItemDto> Items { get; set; } = new();

        public int TotalQuantity { get; set; }

        public decimal SubtotalAmount { get; set; }

        // Server-computed preview decomposition. The storefront renders these values and never
        // performs its own money arithmetic. With no coupon applied DiscountAmount is 0 and
        // FinalAmount is the no-coupon VAT preview.
        public decimal DiscountAmount { get; set; }

        public bool VatEnabled { get; set; }

        public decimal VatRatePercent { get; set; }

        public byte VatCalculationMode { get; set; }

        public decimal VatTaxableAmount { get; set; }

        public decimal VatAmount { get; set; }

        public decimal FinalAmount { get; set; }

        public byte? CurrencyType { get; set; }
    }
}
