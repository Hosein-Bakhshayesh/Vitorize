namespace Vitorize.Web.Models.Storefront
{
    public class StoreOrderModel
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public byte Status { get; set; }
        public byte PaymentStatus { get; set; }
        public decimal SubtotalAmount { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal FinalAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? PaidAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<StoreOrderItemModel> Items { get; set; } = new();
    }

    public class StoreOrderItemModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public Guid? ProductVariantId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }
        public byte DeliveryType { get; set; }
        public byte DeliveryStatus { get; set; }
        public bool RequiresVerification { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? DeliveredAt { get; set; }
        public List<StoreOrderDeliveryModel> Deliveries { get; set; } = new();
    }

    public class StoreOrderDeliveryModel
    {
        public Guid Id { get; set; }
        public Guid OrderItemId { get; set; }
        public byte DeliveryType { get; set; }
        public Guid? GiftCodeId { get; set; }
        public string? DeliveredContent { get; set; }
        public bool IsVisibleToCustomer { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
