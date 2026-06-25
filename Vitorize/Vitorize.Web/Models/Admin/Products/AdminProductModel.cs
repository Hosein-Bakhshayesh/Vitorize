using System.ComponentModel.DataAnnotations;
using Vitorize.Shared.Enums;

namespace Vitorize.Web.Models.Admin.Products
{
    public class AdminProductModel
    {
        public Guid Id { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryTitle { get; set; } = string.Empty;
        public Guid? BrandId { get; set; }
        public string? BrandTitle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }
        public byte ProductType { get; set; }
        public byte DeliveryType { get; set; }
        public byte CurrencyType { get; set; } = (byte)Vitorize.Shared.Enums.CurrencyType.Toman;
        public decimal BasePrice { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice => DiscountPrice.HasValue && DiscountPrice.Value > 0 && DiscountPrice.Value < BasePrice ? DiscountPrice.Value : BasePrice;
        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }
        public int MinOrderQuantity { get; set; } = 1;
        public int? MaxOrderQuantity { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public int SortOrder { get; set; }
        public int AvailableStock { get; set; }
        public bool HasVariants { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateProductRequestModel
    {
        [Required(ErrorMessage = "انتخاب دسته‌بندی الزامی است.")]
        public Guid CategoryId { get; set; }
        public Guid? BrandId { get; set; }
        [Required(ErrorMessage = "عنوان محصول الزامی است.")]
        [MaxLength(250)] public string Title { get; set; } = string.Empty;
        [Required(ErrorMessage = "اسلاگ محصول الزامی است.")]
        [MaxLength(300)] public string Slug { get; set; } = string.Empty;
        [MaxLength(1000)] public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }
        [Range(1, 99, ErrorMessage = "نوع محصول معتبر نیست.")]
        public byte ProductType { get; set; } = (byte)Vitorize.Shared.Enums.ProductType.GiftCard;
        [Range(1, 3, ErrorMessage = "نوع تحویل معتبر نیست.")]
        public byte DeliveryType { get; set; } = (byte)Vitorize.Shared.Enums.DeliveryType.Instant;
        [Range(1, 2, ErrorMessage = "واحد پول فقط می‌تواند ریال یا تومان باشد.")]
        public byte CurrencyType { get; set; } = (byte)Vitorize.Shared.Enums.CurrencyType.Toman;
        [Range(0, double.MaxValue, ErrorMessage = "قیمت پایه معتبر نیست.")]
        public decimal BasePrice { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "قیمت تخفیف معتبر نیست.")]
        public decimal? DiscountPrice { get; set; }
        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }
        [Range(1, 999999)] public int MinOrderQuantity { get; set; } = 1;
        [Range(1, 999999)] public int? MaxOrderQuantity { get; set; }
        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;
        [MaxLength(250)] public string? SeoTitle { get; set; }
        [MaxLength(500)] public string? SeoDescription { get; set; }
        [Range(0, 100000)] public int SortOrder { get; set; }
    }

    public class UpdateProductRequestModel : CreateProductRequestModel { }

    public class ProductLookupModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Slug { get; set; }
    }
}
