namespace Vitorize.Application.Models.Sms
{
    public sealed class SmsHistoryRecordRequest
    {
        public Guid? UserId { get; init; }
        public string Mobile { get; init; } = string.Empty;
        public string Purpose { get; init; } = string.Empty;
        public byte SendType { get; init; }
        public string? TemplateKey { get; init; }
        public int? TemplateId { get; init; }
        public string? PublicReference { get; init; }
        public string? SafeMessagePreview { get; init; }
        public string? InternalNote { get; init; }
        public Guid? CreatedByUserId { get; init; }
        public string? RelatedEntityType { get; init; }
        public Guid? RelatedEntityId { get; init; }
        public string? RelatedEntityReference { get; init; }
        public string? IdempotencyKey { get; init; }
        public Guid? OutboxMessageId { get; init; }
        public int MaxRetryCount { get; init; } = 5;
    }
}
