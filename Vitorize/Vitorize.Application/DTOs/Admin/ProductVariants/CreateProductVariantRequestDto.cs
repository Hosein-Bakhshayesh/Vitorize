namespace Vitorize.Application.DTOs.Admin.ProductVariants
{
    public class CreateProductVariantRequestDto
    {
        public string Title { get; set; } = string.Empty;

        public string? Sku { get; set; }

        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }

        public string? Value { get; set; }

        public byte StockMode { get; set; } = 1;

        public bool IsDefault { get; set; }

        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; }
    }
}