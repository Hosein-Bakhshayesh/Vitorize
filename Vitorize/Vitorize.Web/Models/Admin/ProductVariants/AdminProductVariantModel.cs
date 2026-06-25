using System.ComponentModel.DataAnnotations;
using Vitorize.Shared.Enums;

namespace Vitorize.Web.Models.Admin.ProductVariants
{
    public class AdminProductVariantModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductTitle { get; set; } = string.Empty;
        public byte CurrencyType { get; set; } = (byte)Vitorize.Shared.Enums.CurrencyType.Toman;
        public string Title { get; set; } = string.Empty;
        public string? Sku { get; set; }
        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }
        public decimal FinalPrice => DiscountPrice.HasValue && DiscountPrice.Value > 0 && DiscountPrice.Value < Price ? DiscountPrice.Value : Price;
        public string? Value { get; set; }
        public byte StockMode { get; set; }
        public int AvailableStock { get; set; }
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateProductVariantRequestModel
    {
        [Required(ErrorMessage = "عنوان واریانت الزامی است.")]
        [MaxLength(200)] public string Title { get; set; } = string.Empty;
        [MaxLength(100)] public string? Sku { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "قیمت معتبر نیست.")]
        public decimal Price { get; set; }
        [Range(0, double.MaxValue, ErrorMessage = "قیمت تخفیف معتبر نیست.")]
        public decimal? DiscountPrice { get; set; }
        [MaxLength(100)] public string? Value { get; set; }
        [Range(1, 3, ErrorMessage = "حالت موجودی معتبر نیست.")]
        public byte StockMode { get; set; } = (byte)ProductVariantStockMode.GiftCode;
        public bool IsDefault { get; set; }
        public bool IsActive { get; set; } = true;
        [Range(0, 100000)] public int SortOrder { get; set; } = 10;
    }

    public class UpdateProductVariantRequestModel : CreateProductVariantRequestModel { }
}
