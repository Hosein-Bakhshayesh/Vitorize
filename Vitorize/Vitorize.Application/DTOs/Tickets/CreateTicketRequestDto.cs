namespace Vitorize.Application.DTOs.Tickets
{
    public class CreateTicketRequestDto
    {
        public Guid? OrderId { get; set; }

        public Guid? OrderItemId { get; set; }

        public string Subject { get; set; } = null!;

        public byte Department { get; set; }

        public byte Priority { get; set; }

        public string Message { get; set; } = null!;

        public string? AttachmentPath { get; set; }
    }
}