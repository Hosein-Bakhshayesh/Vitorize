namespace Vitorize.Application.DTOs.Tickets
{
    public class AddTicketMessageRequestDto
    {
        public string Message { get; set; } = null!;

        public string? AttachmentPath { get; set; }
    }
}