namespace Vitorize.Web.Models.Admin.Orders
{
    public class OrderModel
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

        public List<OrderItemModel> Items { get; set; } = new();
    }
}