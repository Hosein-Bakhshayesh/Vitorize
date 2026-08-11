namespace Vitorize.Domain.Entities;

public sealed class KycPolicy
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public ICollection<KycPolicyVersion> Versions { get; set; } = new List<KycPolicyVersion>();
}
