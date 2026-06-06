namespace Vitorize.Web.Models.Admin.Orders
{
    public class OrderItemModel
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

        public List<OrderDeliveryModel> Deliveries { get; set; } = new();
    }
}