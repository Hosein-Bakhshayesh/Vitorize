using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ProductImage
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string ImagePath { get; set; } = null!;

    public string? AltText { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product Product { get; set; } = null!;
}
