using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class ProductVariant
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public string Title { get; set; } = null!;

    public string? Sku { get; set; }

    public decimal Price { get; set; }

    public decimal? DiscountPrice { get; set; }

    public string? Value { get; set; }

    public byte StockMode { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual ICollection<GiftCodeBatch> GiftCodeBatches { get; set; } = new List<GiftCodeBatch>();

    public virtual ICollection<GiftCodeReservation> GiftCodeReservations { get; set; } = new List<GiftCodeReservation>();

    public virtual ICollection<GiftCode> GiftCodes { get; set; } = new List<GiftCode>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual Product Product { get; set; } = null!;
}
