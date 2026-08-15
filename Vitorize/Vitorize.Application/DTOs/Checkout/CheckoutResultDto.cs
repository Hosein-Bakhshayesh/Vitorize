namespace Vitorize.Application.DTOs.Checkout
{
    public class CheckoutResultDto
    {
        public Guid OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public decimal SubtotalAmount { get; set; }

        public decimal DiscountAmount { get; set; }

        public decimal FinalAmount { get; set; }

        // Purchase-time VAT snapshot as persisted on the order.
        public bool VatEnabled { get; set; }

        public decimal VatRatePercent { get; set; }

        public byte VatCalculationMode { get; set; }

        public decimal VatTaxableAmount { get; set; }

        public decimal VatAmount { get; set; }

        public byte CurrencyType { get; set; }

        public byte OrderStatus { get; set; }

        public byte PaymentStatus { get; set; }

        public List<Guid> ReservationIds { get; set; } = new();
    }
}
