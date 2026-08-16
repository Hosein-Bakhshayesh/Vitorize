namespace Vitorize.Web.Models.Admin.Notifications
{
    public class BroadcastPreviewResultModel
    {
        public int RecipientCount { get; set; }
        public int IneligibleCount { get; set; }
        public int MaximumRecipients { get; set; }
        public bool ExceedsLimit { get; set; }
    }

    public class AdminBroadcastModel
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

    /// <summary>A customer chosen for a SelectedCustomers broadcast, held as a removable chip.</summary>
    public class BroadcastRecipientChip
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Mobile { get; set; } = string.Empty;
    }
}
