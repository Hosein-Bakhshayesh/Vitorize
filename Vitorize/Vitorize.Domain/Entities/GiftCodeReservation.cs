using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class GiftCodeReservation
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? OrderItemId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public Guid GiftCodeId { get; set; }

    public byte Status { get; set; }

    public DateTime ReservedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? SoldAt { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public virtual GiftCode GiftCode { get; set; } = null!;

    public virtual Order? Order { get; set; }

    public virtual OrderItem? OrderItem { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual ProductVariant? ProductVariant { get; set; }

    public virtual User User { get; set; } = null!;
}
