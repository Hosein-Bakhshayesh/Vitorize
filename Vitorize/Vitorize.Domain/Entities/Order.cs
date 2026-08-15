using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Order
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string OrderNumber { get; set; } = null!;

    public byte Status { get; set; }

    public byte PaymentStatus { get; set; }

    public decimal SubtotalAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal FinalAmount { get; set; }

    // Purchase-time VAT snapshot. Written once at order creation and never recalculated:
    // later administrative changes to the VAT rate, mode or enabled flag must not alter this order.
    public bool VatEnabled { get; set; }

    public decimal VatRatePercent { get; set; }

    /// <summary>Persisted <c>VatCalculationMode</c>: 1 = BeforeDiscount, 2 = AfterDiscount.</summary>
    public byte VatCalculationMode { get; set; } = 1;

    public decimal VatTaxableAmount { get; set; }

    public decimal VatAmount { get; set; }

    public byte CurrencyType { get; set; }

    public Guid? CouponId { get; set; }

    public string? Description { get; set; }

    public string? AdminNote { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? PaidAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Coupon? Coupon { get; set; }

    public virtual CouponUsage? CouponUsage { get; set; }

    public virtual ICollection<GiftCodeReservation> GiftCodeReservations { get; set; } = new List<GiftCodeReservation>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<OrderStatusHistory> OrderStatusHistories { get; set; } = new List<OrderStatusHistory>();

    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();

    public virtual ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();

    public virtual User User { get; set; } = null!;
}
