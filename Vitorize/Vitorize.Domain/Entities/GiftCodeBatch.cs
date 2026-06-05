using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class GiftCodeBatch
{
    public Guid Id { get; set; }

    public Guid? ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public string BatchTitle { get; set; } = null!;

    public string? SourceName { get; set; }

    public decimal? PurchasePrice { get; set; }

    public string? Notes { get; set; }

    public Guid? ImportedByAdminId { get; set; }

    public DateTime ImportedAt { get; set; }

    public virtual ICollection<GiftCode> GiftCodes { get; set; } = new List<GiftCode>();

    public virtual User? ImportedByAdmin { get; set; }

    public virtual Product? Product { get; set; }

    public virtual ProductVariant? ProductVariant { get; set; }
}
