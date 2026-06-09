namespace Vitorize.Application.DTOs.Notifications
{
    public class NotificationDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Message { get; set; } = null!;

        public byte Type { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? ReadAt { get; set; }
    }
}