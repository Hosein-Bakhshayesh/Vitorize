namespace Vitorize.Domain.Entities;

public sealed class KycPolicyDocumentRequirement
{
    public Guid Id { get; set; }
    public Guid KycPolicyVersionId { get; set; }
    public Guid KycDocumentTypeId { get; set; }
    public bool IsRequired { get; set; }
    public int SortOrder { get; set; }
    public string? Instructions { get; set; }
    public byte RedactionMode { get; set; }
    public string? RedactionInstructions { get; set; }
    public KycPolicyVersion KycPolicyVersion { get; set; } = null!;
    public KycDocumentType KycDocumentType { get; set; } = null!;
}
