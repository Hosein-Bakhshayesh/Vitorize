namespace Vitorize.Domain.Entities;

public partial class PaymentRefund
{
    public Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public Guid OrderId { get; set; }
    public Guid UserId { get; set; }
    public decimal Amount { get; set; }
    public byte Method { get; set; }
    public byte Status { get; set; }
    public string Reason { get; set; } = null!;
    public string IdempotencyKey { get; set; } = null!;
    public Guid? RequestedByUserId { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
    public virtual Payment Payment { get; set; } = null!;
    public virtual Order Order { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual User? RequestedByUser { get; set; }
}

