namespace Vitorize.Application.DTOs.Products
{
    public class ProductDetailDto
    {
        public Guid Id { get; set; }

        public Guid CategoryId { get; set; }

        public Guid? BrandId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }

        public string? FullDescription { get; set; }

        public byte ProductType { get; set; }

        public byte DeliveryType { get; set; }

        public decimal BasePrice { get; set; }

        public decimal? DiscountPrice { get; set; }

        public decimal FinalPrice => DiscountPrice ?? BasePrice;

        public byte CurrencyType { get; set; }

        public bool RequiresVerification { get; set; }

        public bool RequiresSupportMessage { get; set; }

        public int MinOrderQuantity { get; set; }

        public int? MaxOrderQuantity { get; set; }

        public bool IsFeatured { get; set; }

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        public string? ThumbnailImagePath { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;

        public string? BrandTitle { get; set; }

        public List<string> Images { get; set; } = new();

        public List<string> Tags { get; set; } = new();

        public List<ProductVariantDto> Variants { get; set; } = new();

        public int AvailableStock { get; set; }
    }
}