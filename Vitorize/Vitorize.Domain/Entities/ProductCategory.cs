namespace Vitorize.Domain.Entities;

/// <summary>
/// Membership of a product in a category. A product may belong to several categories; this join is
/// the complete set and the only thing category filtering reads.
///
/// <see cref="Product.CategoryId"/> remains the explicit primary category used for the breadcrumb,
/// the canonical URL and SEO metadata, and it is always present here as a membership too, so the two
/// can never disagree about whether a product belongs to a category.
/// </summary>
public partial class ProductCategory
{
    public Guid ProductId { get; set; }

    public Guid CategoryId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Category Category { get; set; } = null!;
}
