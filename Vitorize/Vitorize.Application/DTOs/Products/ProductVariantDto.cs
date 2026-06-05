namespace Vitorize.Application.DTOs.Products
{
    public class ProductVariantDto
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