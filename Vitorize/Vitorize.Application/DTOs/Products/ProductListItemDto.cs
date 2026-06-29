namespace Vitorize.Application.DTOs.Products
{
    public class ProductListItemDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }

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

        public bool IsFeatured { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;

        public string? BrandTitle { get; set; }

        public bool HasVariants { get; set; }

        public int AvailableStock { get; set; }
    }
}