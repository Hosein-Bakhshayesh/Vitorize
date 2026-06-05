using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class OrderItemDelivery
{
    public Guid Id { get; set; }

    public Guid OrderItemId { get; set; }

    public byte DeliveryType { get; set; }

    public Guid? GiftCodeId { get; set; }

    public string? DeliveredContent { get; set; }

    public bool IsVisibleToCustomer { get; set; }

    public Guid? DeliveredByUserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual User? DeliveredByUser { get; set; }

    public virtual GiftCode? GiftCode { get; set; }

    public virtual OrderItem OrderItem { get; set; } = null!;
}
