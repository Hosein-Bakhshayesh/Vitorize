namespace Vitorize.Application.DTOs.Orders
{
    public class DeliveredCodeDto
    {
        public Guid Id { get; set; }

        public Guid OrderId { get; set; }

        public string OrderNumber { get; set; } = string.Empty;

        public Guid OrderItemId { get; set; }

        public Guid ProductId { get; set; }

        public string ProductTitle { get; set; } = string.Empty;

        public string? VariantTitle { get; set; }

        public byte DeliveryType { get; set; }

        public string? DeliveredContent { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
