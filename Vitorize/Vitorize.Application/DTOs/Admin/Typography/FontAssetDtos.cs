namespace Vitorize.Application.DTOs.Admin.Typography;

public sealed class FontAssetDto
{
    public Guid Id { get; set; }
    public string FamilyName { get; set; } = string.Empty;
    public string? FilePath { get; set; }
    public string FileFormat { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public bool IsBuiltIn { get; set; }
    public bool IsActive { get; set; }
    public byte Scope { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class ActivateFontRequestDto
{
    public byte Scope { get; set; }
}
