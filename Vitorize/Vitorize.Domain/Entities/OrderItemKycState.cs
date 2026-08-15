using System;

namespace Vitorize.Domain.Entities;

/// <summary>
/// Mutable operational KYC lifecycle for one purchased item. The immutable KYC
/// requirement remains on <see cref="OrderItem"/> as the purchase-time snapshot.
/// A missing record means the item is not managed by the Phase-2 lifecycle.
/// </summary>
public partial class OrderItemKycState
{
    public Guid Id { get; set; }

    public Guid OrderItemId { get; set; }

    public byte Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public DateTime? SatisfiedAt { get; set; }

    /// <summary>UTC deadline while the customer owns the next KYC action.</summary>
    public DateTime? CustomerActionDeadlineAt { get; set; }

    public Guid? SatisfiedByVerificationProfileId { get; set; }

    public byte[] RowVersion { get; set; } = null!;

    public virtual OrderItem OrderItem { get; set; } = null!;

    public virtual UserVerificationProfile? SatisfiedByVerificationProfile { get; set; }
}
