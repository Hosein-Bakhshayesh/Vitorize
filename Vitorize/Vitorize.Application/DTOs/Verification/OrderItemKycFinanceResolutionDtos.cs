namespace Vitorize.Application.DTOs.Verification;

public sealed class OrderItemKycFinanceResolutionDto
{
    public Guid OrderItemId { get; set; }
    public byte Status { get; set; }
    public string? Reason { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}

public sealed class ResolveOrderItemKycFinanceRequestDto
{
    public string Reason { get; set; } = string.Empty;
    public string? ExternalReference { get; set; }
}
