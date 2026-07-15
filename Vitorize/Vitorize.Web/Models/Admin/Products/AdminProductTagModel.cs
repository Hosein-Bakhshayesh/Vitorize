using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Products;

public sealed class AdminProductTagModel
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Aliases { get; set; }
    public bool IsActive { get; set; }
    public int ProductCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class AdminProductTagInputModel
{
    public Guid? Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; } = string.Empty;
    [Required, StringLength(150), RegularExpression("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    public string Slug { get; set; } = string.Empty;
    [StringLength(1000)] public string? Aliases { get; set; }
    public bool IsActive { get; set; } = true;
}
