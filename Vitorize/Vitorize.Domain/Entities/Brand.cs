using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Brand
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? ImagePath { get; set; }

    public string? ImageAltText { get; set; }

    public string? Description { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? FocusKeyword { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
