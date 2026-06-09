namespace Vitorize.Application.DTOs.Tickets
{
    public class AdminAddTicketMessageRequestDto
    {
        public string Message { get; set; } = null!;

        public string? AttachmentPath { get; set; }

        public bool IsInternalNote { get; set; }
    }
}