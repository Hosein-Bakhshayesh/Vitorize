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

    /// <summary>Customer eligible for a direct KYC-completion reminder.</summary>
    public class KycReminderRecipientDto
    {
        public Guid OrderId { get; set; }
        public Guid UserId { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
        public string OrderNumber { get; set; } = string.Empty;
    }

    /// <summary>Direct KYC reminder for one specific eligible order.</summary>
    public class SendOrderKycReminderRequestDto
    {
        public Guid OrderId { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
