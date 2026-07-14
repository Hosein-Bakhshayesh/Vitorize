namespace Vitorize.Domain.Entities;

public partial class SmsMessageAttempt
{
    public Guid Id { get; set; }
    public Guid SmsMessageId { get; set; }
    public int AttemptNumber { get; set; }
    public byte Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ProviderErrorCode { get; set; }
    public string? ProviderErrorMessage { get; set; }
    public decimal? DeliveryCost { get; set; }
    public DateTime AttemptedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public virtual SmsMessage SmsMessage { get; set; } = null!;
}
