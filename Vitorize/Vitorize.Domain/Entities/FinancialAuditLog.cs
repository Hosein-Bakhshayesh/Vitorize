namespace Vitorize.Domain.Entities;

public partial class FinancialAuditLog
{
    public long Id { get; set; }
    public string EventType { get; set; } = null!;
    public string EntityType { get; set; } = null!;
    public Guid EntityId { get; set; }
    public Guid? UserId { get; set; }
    public decimal? Amount { get; set; }
    public Guid CorrelationId { get; set; }
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual User? User { get; set; }
}

