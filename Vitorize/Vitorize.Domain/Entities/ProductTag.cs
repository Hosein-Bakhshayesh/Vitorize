using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ProductTag
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
