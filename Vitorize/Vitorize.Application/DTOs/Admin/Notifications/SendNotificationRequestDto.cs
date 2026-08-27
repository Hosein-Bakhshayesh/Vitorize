namespace Vitorize.Application.DTOs.Admin.Notifications
{
    public class SendNotificationRequestDto
    {
        public Guid UserId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        /// <summary>When enabled, queue the notification body as an SMS as well.</summary>
        public bool SendSms { get; set; }
    }
}
