using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class OrderStatusHistory
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public byte? FromStatus { get; set; }

    public byte ToStatus { get; set; }

    public Guid? ChangedByUserId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual Order Order { get; set; } = null!;
}
