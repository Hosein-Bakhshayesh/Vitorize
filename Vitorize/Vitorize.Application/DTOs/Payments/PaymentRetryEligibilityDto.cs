namespace Vitorize.Application.DTOs.Payments;

public sealed class PaymentRetryEligibilityDto
{
    public Guid OrderId { get; init; }
    public bool CanRetry { get; init; }
    public string? Reason { get; init; }
}
