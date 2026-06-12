namespace Vitorize.Application.DTOs.Outbox
{
    public class NotificationCreatedEventDto
    {
        public Guid NotificationId { get; set; }

        public Guid UserId { get; set; }

        public byte Type { get; set; }

        public string Title { get; set; } = null!;

        public string Message { get; set; } = null!;

        public DateTime CreatedAt { get; set; }
    }
}