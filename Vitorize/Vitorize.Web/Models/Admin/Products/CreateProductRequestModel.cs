using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Products
{
    public class CreateProductRequestModel
    {
        [Required(ErrorMessage = "دسته‌بندی الزامی است.")]
        public Guid CategoryId { get; set; }

        public Guid? BrandId { get; set; }

        [Required(ErrorMessage = "عنوان محصول الزامی است.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسلاگ محصول الزامی است.")]
        public string Slug { get; set; } = string.Empty;

        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public string? ThumbnailImagePath { get; set; }

        public byte ProductType { get; set; } = 1;
        public byte DeliveryType { get; set; } = 1;
        public byte CurrencyType { get; set; } = 1;

        [Range(0, double.MaxValue, ErrorMessage = "قیمت پایه نامعتبر است.")]
        public decimal BasePrice { get; set; }

        public decimal? DiscountPrice { get; set; }

        public bool RequiresVerification { get; set; }
        public bool RequiresSupportMessage { get; set; }

        public int MinOrderQuantity { get; set; } = 1;
        public int? MaxOrderQuantity { get; set; }

        public bool IsFeatured { get; set; }
        public bool IsActive { get; set; } = true;

        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
    }
}