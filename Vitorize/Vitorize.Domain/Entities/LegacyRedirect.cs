namespace Vitorize.Domain.Entities;

public sealed class LegacyRedirect
{
    public Guid Id { get; set; }
    public string SourcePath { get; set; } = null!;
    public string? DestinationPath { get; set; }
    public short StatusCode { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
