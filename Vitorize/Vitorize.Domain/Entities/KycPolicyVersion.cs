namespace Vitorize.Domain.Entities;

public sealed class KycPolicyVersion
{
    public Guid Id { get; set; }
    public Guid KycPolicyId { get; set; }
    public int Version { get; set; }
    public byte Status { get; set; }
    public string CustomerTitle { get; set; } = null!;
    public string? CustomerInstructions { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public KycPolicy KycPolicy { get; set; } = null!;
    public ICollection<KycPolicyDocumentRequirement> DocumentRequirements { get; set; } = new List<KycPolicyDocumentRequirement>();
}
