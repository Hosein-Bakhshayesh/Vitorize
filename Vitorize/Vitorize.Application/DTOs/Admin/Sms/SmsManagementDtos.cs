using Vitorize.Shared.Common;

namespace Vitorize.Application.DTOs.Admin.Sms
{
    public sealed class SmsHistoryFilterDto
    {
        public string? Search { get; set; }
        public byte? Status { get; set; }
        public byte? SendType { get; set; }
        public string? Purpose { get; set; }
        public string? Provider { get; set; }
        public string? TemplateKey { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public bool OnlyFailed { get; set; }
        public bool OnlyPending { get; set; }
        public bool OnlyCustom { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 25;
    }

    public sealed class SmsHistoryItemDto
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
        public IReadOnlyList<SmsAttemptDto> Attempts { get; set; } = [];
    }

    public sealed class SmsAttemptDto
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

    public sealed class SmsSummaryDto
    {
        public int SentToday { get; set; }
        public int FailedToday { get; set; }
        public int PendingOrRetrying { get; set; }
        public int OtpMessages { get; set; }
        public int NotificationMessages { get; set; }
        public int CustomMessages { get; set; }
    }

    public sealed class SmsHealthDto
    {
        public bool IsEnabled { get; set; }
        public bool IsConfigured { get; set; }
        public bool ConnectionOk { get; set; }
        public decimal? Credit { get; set; }
        public IReadOnlyList<long> Lines { get; set; } = [];
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

    public sealed class SendCustomNotificationRequestDto
    {
        public string Mobile { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public string? InternalNote { get; set; }
        public bool SendImmediately { get; set; }
        public string? IdempotencyKey { get; set; }
    }

    public sealed class SendCustomTextRequestDto
    {
        public string Mobile { get; set; } = string.Empty;
        public Guid? UserId { get; set; }
        public string Text { get; set; } = string.Empty;
        public string? InternalNote { get; set; }
        public bool SendImmediately { get; set; }
        public string? IdempotencyKey { get; set; }
    }

    public sealed class SmsActionResultDto
    {
        public Guid SmsMessageId { get; set; }
        public bool Queued { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
