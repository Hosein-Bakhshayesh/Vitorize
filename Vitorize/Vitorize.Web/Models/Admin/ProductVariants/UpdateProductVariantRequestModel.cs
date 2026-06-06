using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.ProductVariants
{
    public class UpdateProductVariantRequestModel
    {
        [Required(ErrorMessage = "عنوان تنوع الزامی است.")]
        public string Title { get; set; } = string.Empty;

        public string? Sku { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "قیمت نامعتبر است.")]
        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }

        public string? Value { get; set; }

        public byte StockMode { get; set; } = 1;

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }
    }
}