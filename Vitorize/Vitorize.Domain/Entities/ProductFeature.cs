namespace Vitorize.Domain.Entities;

public partial class ProductFeature
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string Title { get; set; } = null!;
    public string Value { get; set; } = null!;
    public string? IconKey { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public virtual Product Product { get; set; } = null!;
}
