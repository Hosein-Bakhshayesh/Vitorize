namespace Vitorize.Domain.Entities;

public partial class SmsMessage
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string Mobile { get; set; } = null!;
    public string MaskedMobile { get; set; } = null!;
    public string Purpose { get; set; } = null!;
    public byte SendType { get; set; }
    public string? TemplateKey { get; set; }
    public int? TemplateId { get; set; }
    public string? PublicReference { get; set; }
    public string? SafeMessagePreview { get; set; }
    public string? InternalNote { get; set; }
    public string Provider { get; set; } = null!;
    public string? ProviderMessageId { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? ProviderErrorMessage { get; set; }
    public decimal? DeliveryCost { get; set; }
    public byte Status { get; set; }
    public int RetryCount { get; set; }
    public int MaxRetryCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? FailedAt { get; set; }
    public DateTime? NextRetryAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityReference { get; set; }
    public string IdempotencyKey { get; set; } = null!;
    public Guid CorrelationId { get; set; }
    public Guid? OutboxMessageId { get; set; }

    public virtual User? User { get; set; }
    public virtual User? CreatedByUser { get; set; }
    public virtual ICollection<SmsMessageAttempt> Attempts { get; set; } = new List<SmsMessageAttempt>();
}
