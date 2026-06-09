namespace Vitorize.Application.DTOs.Tickets
{
    public class TicketDto
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }

        public Guid? OrderId { get; set; }

        public string Subject { get; set; } = null!;

        public byte Department { get; set; }

        public byte Priority { get; set; }

        public byte Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public DateTime? ClosedAt { get; set; }

        public List<TicketMessageDto> Messages { get; set; } = new();
    }
}