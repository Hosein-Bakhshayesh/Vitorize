namespace Vitorize.Application.DTOs.Orders;

/// <summary>Read-only, purchase-snapshot KYC representation for one order item.</summary>
public sealed class OrderItemKycProjectionDto
{
    public bool RequiresKyc { get; set; }
    public byte? LifecycleStatus { get; set; }
    public string? LifecycleLabel { get; set; }
    public bool BlocksFulfillment { get; set; }
    public string CustomerAction { get; set; } = "None";
    public string CustomerActionLabel { get; set; } = string.Empty;
    public Guid? PolicyVersionId { get; set; }
    public string? PolicyTitle { get; set; }
    public string? PolicyInstructions { get; set; }
    public decimal EvaluatedAmount { get; set; }
    public decimal? ThresholdAmount { get; set; }
    public int? CustomerActionDeadlineHours { get; set; }
    public DateTime? CustomerActionDeadlineAt { get; set; }
    public bool IsCustomerActionOverdue { get; set; }
    public bool IsFulfilled { get; set; }
    public bool HasSupportWork { get; set; }
    public byte? FinanceResolutionStatus { get; set; }
    public List<OrderItemKycDocumentRequirementDto> Documents { get; set; } = new();
}

public sealed class OrderItemKycDocumentRequirementDto
{
    public Guid DocumentTypeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public bool IsRequired { get; set; }
    public byte RedactionMode { get; set; }
    public string? RedactionInstructions { get; set; }
    public string UploadStatus { get; set; } = "Missing";
}
