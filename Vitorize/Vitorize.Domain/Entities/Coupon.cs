using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Coupon
{
    public Guid Id { get; set; }

    public string Code { get; set; } = null!;

    public string Title { get; set; } = null!;

    public byte DiscountType { get; set; }

    public decimal DiscountValue { get; set; }

    public int? MaxUsageCount { get; set; }

    public int UsedCount { get; set; }

    public int? MaxUsagePerUser { get; set; }

    public decimal? MinOrderAmount { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual ICollection<CouponUsage> CouponUsages { get; set; } = new List<CouponUsage>();

    public virtual ICollection<Order> Orders { get; set; } = new List<Order>();
}
