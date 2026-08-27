using Vitorize.Application.Common;

namespace Vitorize.Application.DTOs.Orders
{
    public class OrderDto : ICustomerOrderFacts
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
        // Aggregate KYC status for administrative order lists. The per-item
        // lifecycle remains available in Items for detailed views.
        public bool RequiresVerification { get; set; }
        public bool VerificationCompleted { get; set; }
        public List<OrderItemDto> Items { get; set; } = new();

        // Customer self-service, decided by the server only. The web UI must never infer these from
        // the status bytes: cancellability also depends on whether a gateway session can still
        // settle, which the browser cannot see. Null reason means the action is available.
        public bool CanCustomerCancel { get; set; }
        public string? CustomerCancelBlockReason { get; set; }
        public bool CanCustomerHide { get; set; }

        IEnumerable<ICustomerOrderItemFacts> ICustomerOrderFacts.ItemFacts => Items;
    }
}
