namespace Vitorize.Application.DTOs.Tickets
{
    public class TicketDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public string UserFullName { get; set; } = string.Empty;
        public string UserMobile { get; set; } = string.Empty;

        public Guid? OrderId { get; set; }

        public string Subject { get; set; } = null!;

        public byte Department { get; set; }

        public byte Priority { get; set; }

        public byte Status { get; set; }
        public bool IsFulfillmentTicket { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public List<TicketMessageDto> Messages { get; set; } = new();
        public List<TicketOrderItemDto> FulfillmentItems { get; set; } = new();
    }

    public sealed class TicketOrderItemDto
    {
        public Guid Id { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public string? VariantTitle { get; set; }
        public int Quantity { get; set; }
        public byte DeliveryType { get; set; }
        public byte DeliveryStatus { get; set; }
    }
}
