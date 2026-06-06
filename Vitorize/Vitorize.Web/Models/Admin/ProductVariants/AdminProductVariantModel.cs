namespace Vitorize.Web.Models.Admin.ProductVariants
{
    public class AdminProductVariantModel
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }

        public string Title { get; set; } = string.Empty;
        public string? Sku { get; set; }

        public decimal Price { get; set; }
        public decimal? DiscountPrice { get; set; }

        public string? Value { get; set; }

        public byte StockMode { get; set; }

        public bool IsDefault { get; set; }
        public bool IsActive { get; set; }

        public int SortOrder { get; set; }

        public int AvailableStock { get; set; }

        public DateTime CreatedAt { get; set; }

        public decimal FinalPrice => DiscountPrice ?? Price;
    }
}