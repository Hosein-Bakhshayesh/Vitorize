namespace Vitorize.Application.DTOs.Tickets
{
    public class TicketMessageDto
    {
        public Guid Id { get; set; }

        public Guid TicketId { get; set; }

        public Guid SenderUserId { get; set; }

        public string Message { get; set; } = null!;

        public string? AttachmentPath { get; set; }

        public bool IsInternalNote { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}