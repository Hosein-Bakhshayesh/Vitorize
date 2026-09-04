namespace Vitorize.Application.DTOs.Admin.Products
{
    public class AdminProductDto
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public Guid? BrandId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? RedirectUrl { get; set; }

        public byte ProductType { get; set; }
        public byte DeliveryType { get; set; }

        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }

        public byte CurrencyType { get; set; }

        public bool RequiresSupportMessage { get; set; }

        public int MinOrderQuantity { get; set; }
        public int? MaxOrderQuantity { get; set; }

        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public string? ThumbnailAltText { get; set; }
        public List<Guid> TagIds { get; set; } = new();

        public int SortOrder { get; set; }

        public string CategoryTitle { get; set; } = string.Empty;
        public string? BrandTitle { get; set; }
        public decimal FinalPrice { get; set; }
        public int AvailableStock { get; set; }
        public bool HasVariants { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<Vitorize.Application.DTOs.Products.ProductFeatureDto> Features { get; set; } = new();
        public List<Vitorize.Application.DTOs.Products.ProductInputFieldDto> InputFields { get; set; } = new();

        /// <summary>Takes the product off sale without touching a single unit of its inventory.</summary>
        public bool ForceOutOfStock { get; set; }

        /// <summary>Every category the product belongs to, primary included.</summary>
        public List<Guid> CategoryIds { get; set; } = new();

        /// <summary>Category titles in the same order, for display without a second round trip.</summary>
        public List<string> CategoryTitles { get; set; } = new();
}
}
