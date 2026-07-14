namespace Vitorize.Domain.Entities;

public partial class FontAsset
{
    public Guid Id { get; set; }
    public string FamilyName { get; set; } = null!;
    public string? FilePath { get; set; }
    public string FileFormat { get; set; } = null!;
    public string? MimeType { get; set; }
    public long SizeBytes { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsActive { get; set; }
    public byte Scope { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual User? CreatedByUser { get; set; }
}
