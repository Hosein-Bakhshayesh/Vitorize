namespace Vitorize.Web.Models.Admin.Products
{
    public class AdminProductModel
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public Guid? BrandId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }

        public byte ProductType { get; set; }
        public byte DeliveryType { get; set; }
        public byte CurrencyType { get; set; }

        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }

        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }

        public int MinOrderQuantity { get; set; }
        public int? MaxOrderQuantity { get; set; }

        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;
        public string? BrandTitle { get; set; }

        public bool HasVariants { get; set; }
        public int AvailableStock { get; set; }

        public DateTime CreatedAt { get; set; }

        public decimal FinalPrice => DiscountPrice ?? BasePrice;
    }
}