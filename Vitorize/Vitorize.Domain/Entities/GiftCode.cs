using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class GiftCode
{
    public Guid Id { get; set; }

    public Guid? BatchId { get; set; }

    public Guid ProductId { get; set; }

    public Guid? ProductVariantId { get; set; }

    public string EncryptedCode { get; set; } = null!;

    public string? MaskedCode { get; set; }

    public string? SerialNumber { get; set; }

    public string? ExtraData { get; set; }

    public byte Status { get; set; }

    public Guid? ReservedByUserId { get; set; }

    public DateTime? ReservationExpiresAt { get; set; }

    public int EncryptionVersion { get; set; }

    public string? CodeHashFingerprint { get; set; }

    public DateTime? ReservedAt { get; set; }

    public DateTime? SoldAt { get; set; }

    public DateTime? DeliveredAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? OrderItemId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual GiftCodeBatch? Batch { get; set; }

    public virtual GiftCodeReservation? GiftCodeReservation { get; set; }

    public virtual OrderItem? OrderItem { get; set; }

    public virtual ICollection<OrderItemDelivery> OrderItemDeliveries { get; set; } = new List<OrderItemDelivery>();

    public virtual Product Product { get; set; } = null!;

    public virtual ProductVariant? ProductVariant { get; set; }

    public virtual User? ReservedByUser { get; set; }
}
