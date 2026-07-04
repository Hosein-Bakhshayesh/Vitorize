namespace Vitorize.Application.DTOs.Wishlist
{
    public class WishlistItemDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ThumbnailImagePath { get; set; }

        public decimal BasePrice { get; set; }

        public decimal? DiscountPrice { get; set; }

        public decimal FinalPrice =>
            DiscountPrice.HasValue &&
            DiscountPrice.Value > 0 &&
            DiscountPrice.Value < BasePrice
                ? DiscountPrice.Value
                : BasePrice;

        public byte ProductType { get; set; }

        public byte DeliveryType { get; set; }

        public byte CurrencyType { get; set; }

        public bool RequiresVerification { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;

        public string? BrandTitle { get; set; }

        public bool HasVariants { get; set; }

        public bool IsAvailable { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
