namespace Vitorize.Application.DTOs.Orders
{
    public class OrderDeliveryDto
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