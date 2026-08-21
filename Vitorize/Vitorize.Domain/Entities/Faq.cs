using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Faq
{
    public Guid Id { get; set; }

    /// <summary>
    /// Null for the site-wide FAQ; set for an entry that belongs to one product. One entity keeps a
    /// single sanitisation and ordering path for both, so a product answer can never be rendered
    /// through a laxer route than a global one.
    /// </summary>
    public Guid? ProductId { get; set; }

    public string Question { get; set; } = null!;

    public string Answer { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual Product? Product { get; set; }
}
