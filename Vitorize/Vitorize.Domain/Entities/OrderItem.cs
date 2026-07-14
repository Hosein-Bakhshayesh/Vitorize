using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public string ProductTitle { get; set; } = null!;

    public string? VariantTitle { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal TotalPrice { get; set; }

    public byte DeliveryType { get; set; }

    public byte DeliveryStatus { get; set; }

    public bool RequiresVerification { get; set; }

    public Guid? SupportTicketId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public virtual ICollection<GiftCodeReservation> GiftCodeReservations { get; set; } = new List<GiftCodeReservation>();

    public virtual ICollection<GiftCode> GiftCodes { get; set; } = new List<GiftCode>();

    public virtual Order Order { get; set; } = null!;

    public virtual ICollection<OrderItemDelivery> OrderItemDeliveries { get; set; } = new List<OrderItemDelivery>();

    public virtual ICollection<OrderItemInputValue> InputValues { get; set; } = new List<OrderItemInputValue>();

    public virtual Product Product { get; set; } = null!;

    public virtual ProductVariant? ProductVariant { get; set; }

    public virtual Ticket? SupportTicket { get; set; }
}
