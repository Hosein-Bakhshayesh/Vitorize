namespace Vitorize.Application.DTOs.Orders;

/// <summary>Safe payment information displayed in an order's administrative detail view.</summary>
public sealed class OrderPaymentDto
{
    public Guid Id { get; set; }
    public decimal Amount { get; set; }
    public string Gateway { get; set; } = string.Empty;
    public string? ReferenceNumber { get; set; }
    public string? MaskedCardPan { get; set; }
    public byte Status { get; set; }
    public DateTime? VerifiedAt { get; set; }
}
