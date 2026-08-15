namespace Vitorize.Domain.Entities;

public partial class OrderItemKycFinanceResolution
{
    public Guid Id { get; set; }
    public Guid OrderItemId { get; set; }
    public byte Status { get; set; }
    public string? Reason { get; set; }
    public string? ExternalReference { get; set; }
    public Guid? ResolvedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public virtual OrderItem OrderItem { get; set; } = null!;
    public virtual User? ResolvedByUser { get; set; }
}
