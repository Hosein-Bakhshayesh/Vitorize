namespace Vitorize.Web.Models.Admin.Sms
{
    public sealed class SmsHistoryModel
    {
        public Guid Id { get; set; }
        public string Mobile { get; set; } = string.Empty;
        public string MaskedMobile { get; set; } = string.Empty;
        public string Purpose { get; set; } = string.Empty;
        public byte SendType { get; set; }
        public string SendTypeName { get; set; } = string.Empty;
        public string? TemplateKey { get; set; }
        public int? TemplateId { get; set; }
        public string? PublicReference { get; set; }
        public string Provider { get; set; } = string.Empty;
        public byte Status { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? ProviderMessageId { get; set; }
        public string? ProviderErrorCode { get; set; }
        public string? ProviderErrorMessage { get; set; }
        public decimal? DeliveryCost { get; set; }
        public int RetryCount { get; set; }
        public int MaxRetryCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastAttemptAt { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? FailedAt { get; set; }
        public DateTime? NextRetryAt { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public string? RelatedEntityType { get; set; }
        public Guid? RelatedEntityId { get; set; }
        public string? RelatedEntityReference { get; set; }
        public string? SafeMessagePreview { get; set; }
        public string? InternalNote { get; set; }
        public Guid CorrelationId { get; set; }
        public List<SmsAttemptModel> Attempts { get; set; } = new();
    }

    public sealed class SmsAttemptModel
    {
        public int AttemptNumber { get; set; }
        public string StatusName { get; set; } = string.Empty;
        public string? ProviderMessageId { get; set; }
        public string? ProviderErrorCode { get; set; }
        public string? ProviderErrorMessage { get; set; }
        public decimal? DeliveryCost { get; set; }
        public DateTime AttemptedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public sealed class SmsSummaryModel
    {
        public int SentToday { get; set; }
        public int FailedToday { get; set; }
        public int PendingOrRetrying { get; set; }
        public int OtpMessages { get; set; }
        public int NotificationMessages { get; set; }
        public int CustomMessages { get; set; }
    }

    public sealed class SmsHealthModel
    {
        public bool IsEnabled { get; set; }
        public bool IsConfigured { get; set; }
        public bool ConnectionOk { get; set; }
        public decimal? Credit { get; set; }
        public List<long> Lines { get; set; } = new();
        public int? OtpTemplateId { get; set; }
        public int? NotificationTemplateId { get; set; }
        public int PendingOutboxCount { get; set; }
        public int FailedOutboxCount { get; set; }
        public bool CustomSendEnabled { get; set; }
        public bool CustomTextEnabled { get; set; }
        public bool AllowImmediateSend { get; set; }
        public bool AllowRetryFailed { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public sealed class SmsActionResultModel
    {
        public Guid SmsMessageId { get; set; }
        public bool Queued { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
