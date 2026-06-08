namespace Vitorize.Web.Models.Storefront
{
    public class StoreProductFilterModel
    {
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        public Guid? BrandId { get; set; }
        public bool? IsFeatured { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
    }

    public class StoreProductLookupModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }

    public class StoreProductCardModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice => DiscountPrice ?? BasePrice;
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

    public class StoreProductDetailsModel : StoreProductCardModel
    {
        public Guid CategoryId { get; set; }
        public Guid? BrandId { get; set; }
        public string? FullDescription { get; set; }
        public bool RequiresSupportMessage { get; set; }
        public int MinOrderQuantity { get; set; } = 1;
        public int? MaxOrderQuantity { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public List<string> Images { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<StoreProductVariantModel> Variants { get; set; } = new();
    }

    public class StoreProductVariantModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice => DiscountPrice ?? Price;
        public string? Value { get; set; }
        public byte StockMode { get; set; }
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public int AvailableStock { get; set; }
    }
}
