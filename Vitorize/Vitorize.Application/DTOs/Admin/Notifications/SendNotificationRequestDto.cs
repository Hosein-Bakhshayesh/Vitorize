namespace Vitorize.Application.DTOs.Admin.Notifications
{
    public class SendNotificationRequestDto
    {
        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
