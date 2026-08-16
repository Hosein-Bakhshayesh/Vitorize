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

        /// <summary>
        /// Internal call-to-action path for an announcement, projected from its broadcast.
        /// Null for transactional and direct system notifications.
        /// </summary>
        public string? ActionUrl { get; set; }
    }
}