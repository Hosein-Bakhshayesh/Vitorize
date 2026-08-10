using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Cart
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    /// <summary>SHA-256 fingerprint of the opaque guest-cart bearer secret.</summary>
    public string? GuestTokenHash { get; set; }

    /// <summary>Used exclusively for guest-cart expiry. Authenticated carts are never cleaned by it.</summary>
    public DateTime? LastActivityAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual User? User { get; set; }
}
