namespace Vitorize.Web.Models.Store
{
    public class StoreProductModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }
        public byte ProductType { get; set; }
        public byte DeliveryType { get; set; }
        public byte CurrencyType { get; set; }
        public bool RequiresVerification { get; set; }
        public bool IsFeatured { get; set; }
        public string CategoryTitle { get; set; } = string.Empty;
        public string? BrandTitle { get; set; }
        public bool HasVariants { get; set; }
        public int AvailableStock { get; set; }
        public List<StoreProductFeatureModel> Features { get; set; } = new();
        public List<StoreProductInputFieldModel> InputFields { get; set; } = new();
        public double AverageRating { get; set; }
        public int ReviewCount { get; set; }

        public decimal FinalPrice => DiscountPrice is > 0 && DiscountPrice < BasePrice ? DiscountPrice.Value : BasePrice;
        public bool HasDiscount => DiscountPrice is > 0 && DiscountPrice < BasePrice;
        public int DiscountPercent => HasDiscount && BasePrice > 0
            ? (int)Math.Round((BasePrice - DiscountPrice!.Value) / BasePrice * 100)
            : 0;
    }

    public class StoreProductFeatureModel
    {
        public Guid? Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? IconKey { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class StoreProductInputFieldModel
    {
        public Guid? Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Placeholder { get; set; }
        public byte FieldType { get; set; }
        public bool IsRequired { get; set; }
        public List<string> Options { get; set; } = new();
        public string? DefaultValue { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public string? ValidationMessage { get; set; }
        public bool IsSensitive { get; set; }
        public bool RequiresConfirmation { get; set; }
        public byte DisplayStage { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
    }

    public class StoreProductDetailModel
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
        public byte CurrencyType { get; set; }
        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }
        public int MinOrderQuantity { get; set; } = 1;
        public int? MaxOrderQuantity { get; set; }
        public bool IsFeatured { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public string CategoryTitle { get; set; } = string.Empty;
        public string? BrandTitle { get; set; }
        public List<string> Images { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<StoreVariantModel> Variants { get; set; } = new();
        public int AvailableStock { get; set; }
        public List<StoreProductFeatureModel> Features { get; set; } = new();
        public List<StoreProductInputFieldModel> InputFields { get; set; } = new();

        public decimal FinalPrice => DiscountPrice is > 0 && DiscountPrice < BasePrice ? DiscountPrice.Value : BasePrice;
        public bool HasDiscount => DiscountPrice is > 0 && DiscountPrice < BasePrice;
    }

    public class StoreVariantModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public string? Value { get; set; }
        public byte StockMode { get; set; }
        public bool IsDefault { get; set; }
        public int SortOrder { get; set; }
        public int AvailableStock { get; set; }

        public decimal FinalPrice => DiscountPrice is > 0 && DiscountPrice < Price ? DiscountPrice.Value : Price;
    }

    public class StoreLookupModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }

    public class StoreHomeModel
    {
        public List<StoreBannerModel> Banners { get; set; } = new();
        public List<StoreCategoryModel> Categories { get; set; } = new();
        public List<StoreBrandModel> Brands { get; set; } = new();
        public List<StoreProductModel> FeaturedProducts { get; set; } = new();
        public List<StoreBlogPostModel> LatestBlogPosts { get; set; } = new();
        public List<StoreFaqModel> Faqs { get; set; } = new();
    }

    public class StoreBannerModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string? MobileImagePath { get; set; }
        public string? LinkUrl { get; set; }
        public string Position { get; set; } = string.Empty;
        public int SortOrder { get; set; }
    }

    public class StoreCategoryModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? ImagePath { get; set; }
    }

    public class StoreBrandModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ImagePath { get; set; }
    }

    public class StoreBlogPostModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? CoverImagePath { get; set; }
        public string? ContentHtml { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class StoreFaqModel
    {
        public Guid Id { get; set; }
        public string Question { get; set; } = string.Empty;
        public string Answer { get; set; } = string.Empty;
    }
}
