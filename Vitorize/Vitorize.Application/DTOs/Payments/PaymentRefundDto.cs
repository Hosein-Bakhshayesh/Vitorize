namespace Vitorize.Application.DTOs.Payments;

public sealed class PaymentRefundRequestDto
{
    public byte Method { get; set; } = 1;
    public string Reason { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed class CompletePaymentRefundRequestDto
{
    public string? GatewayReference { get; set; }
}

public sealed class PaymentRefundDto
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
    public byte Method { get; set; }
    public byte Status { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
