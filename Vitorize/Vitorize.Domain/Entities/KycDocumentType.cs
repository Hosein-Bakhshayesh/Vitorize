namespace Vitorize.Domain.Entities;

public sealed class KycDocumentType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public string AllowedExtensions { get; set; } = "jpg,jpeg,png,webp";
    public long MaxFileSizeBytes { get; set; } = 5242880;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<KycPolicyDocumentRequirement> PolicyRequirements { get; set; } = new List<KycPolicyDocumentRequirement>();
    public ICollection<VerificationDocument> VerificationDocuments { get; set; } = new List<VerificationDocument>();
}
