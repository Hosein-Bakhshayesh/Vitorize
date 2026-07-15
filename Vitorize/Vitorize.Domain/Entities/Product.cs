using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Product
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public Guid? BrandId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? ShortDescription { get; set; }

    public string? FullDescription { get; set; }

    public byte ProductType { get; set; }

    public byte DeliveryType { get; set; }

    public decimal BasePrice { get; set; }

    public decimal? DiscountPrice { get; set; }

    public byte CurrencyType { get; set; }

    public bool RequiresVerification { get; set; }

    public bool RequiresSupportMessage { get; set; }

    public int MinOrderQuantity { get; set; }

    public int? MaxOrderQuantity { get; set; }

    public bool IsFeatured { get; set; }

    public bool IsActive { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? FocusKeyword { get; set; }

    public string? ThumbnailAltText { get; set; }

    public string? ThumbnailImagePath { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual Brand? Brand { get; set; }

    public virtual ICollection<CartItem> CartItems { get; set; } = new List<CartItem>();

    public virtual Category Category { get; set; } = null!;

    public virtual ICollection<GiftCodeBatch> GiftCodeBatches { get; set; } = new List<GiftCodeBatch>();

    public virtual ICollection<GiftCodeReservation> GiftCodeReservations { get; set; } = new List<GiftCodeReservation>();

    public virtual ICollection<GiftCode> GiftCodes { get; set; } = new List<GiftCode>();

    public virtual ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();

    public virtual ICollection<ProductImage> ProductImages { get; set; } = new List<ProductImage>();

    public virtual ICollection<ProductFeature> ProductFeatures { get; set; } = new List<ProductFeature>();

    public virtual ICollection<ProductInputField> ProductInputFields { get; set; } = new List<ProductInputField>();

    public virtual ICollection<ProductReview> ProductReviews { get; set; } = new List<ProductReview>();

    public virtual ICollection<ProductVariant> ProductVariants { get; set; } = new List<ProductVariant>();

    public virtual ICollection<WishList> WishLists { get; set; } = new List<WishList>();

    public virtual ICollection<ProductTag> Tags { get; set; } = new List<ProductTag>();
}
