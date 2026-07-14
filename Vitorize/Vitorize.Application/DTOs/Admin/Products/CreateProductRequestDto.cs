namespace Vitorize.Application.DTOs.Admin.Products
{
    public class CreateProductRequestDto
    {
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

        // پیش‌فرض ریال
        public byte CurrencyType { get; set; } = 1;

        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }

        public int MinOrderQuantity { get; set; } = 1;
        public int? MaxOrderQuantity { get; set; }

        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }

        public int SortOrder { get; set; }
        public List<Vitorize.Application.DTOs.Products.ProductFeatureDto> Features { get; set; } = new();
        public List<Vitorize.Application.DTOs.Products.ProductInputFieldDto> InputFields { get; set; } = new();
    }
}
