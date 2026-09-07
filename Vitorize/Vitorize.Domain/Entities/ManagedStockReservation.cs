namespace Vitorize.Domain.Entities;

/// <summary>Quantity hold for a counted (non gift-code) product variant.</summary>
public partial class ManagedStockReservation
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid OrderItemId { get; set; }
    public Guid ProductVariantId { get; set; }
    public int Quantity { get; set; }
    public byte Status { get; set; }
    public DateTime ReservedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? ReleasedAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
}
