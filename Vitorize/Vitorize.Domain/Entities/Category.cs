using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Category
{
    public Guid Id { get; set; }

    public Guid? ParentId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string? ImagePath { get; set; }

    public string? Icon { get; set; }

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? FocusKeyword { get; set; }

    public string? ImageAltText { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Category> InverseParent { get; set; } = new List<Category>();

    public virtual Category? Parent { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
