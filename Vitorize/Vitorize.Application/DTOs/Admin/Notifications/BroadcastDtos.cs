namespace Vitorize.Application.DTOs.Admin.Notifications
{
    /// <summary>Recipient-count preview. Informational only; Send re-resolves recipients server-side.</summary>
    public class BroadcastPreviewRequestDto
    {
        public byte Audience { get; set; }
        public List<Guid> SelectedCustomerIds { get; set; } = new();
    }

    public class BroadcastPreviewResultDto
    {
        public int RecipientCount { get; set; }
        /// <summary>Deduplicated selected ids that are not eligible customers.</summary>
        public int IneligibleCount { get; set; }
        public int MaximumRecipients { get; set; }
        public bool ExceedsLimit { get; set; }
    }

    public class SendBroadcastRequestDto
    {
        public byte Audience { get; set; }
        public List<Guid> SelectedCustomerIds { get; set; } = new();
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        /// <summary>When enabled, queue the same notification body for every recipient by SMS.</summary>
        public bool SendSms { get; set; }
        /// <summary>Optional internal, relative path. Validated by NotificationActionUrlRules.</summary>
        public string? ActionUrl { get; set; }
    }

    public class BroadcastDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public byte AudienceType { get; set; }
        public int RecipientCount { get; set; }
        public byte Status { get; set; }
        public string? ActionUrl { get; set; }
        public Guid CreatedByUserId { get; set; }
        public string CreatedByFullName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? SentAt { get; set; }
    }
}
