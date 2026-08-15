namespace Vitorize.Application.DTOs.Orders
{
    public class OrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        // Purchase-time VAT snapshot. Historical orders created before FIX-13 report
        // VatEnabled = false with zero amounts, so no fictional VAT row is ever shown.
        public bool VatEnabled { get; set; }
        public decimal VatRatePercent { get; set; }
        public byte VatCalculationMode { get; set; }
        public decimal VatTaxableAmount { get; set; }
        public decimal VatAmount { get; set; }
        public byte CurrencyType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();
    }
}
