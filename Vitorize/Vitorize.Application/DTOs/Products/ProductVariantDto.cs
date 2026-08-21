namespace Vitorize.Application.DTOs.Products
{
    public class ProductVariantDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string? Sku { get; set; }

        public decimal Price { get; set; }

        public decimal? DiscountPrice { get; set; }

        public decimal FinalPrice =>
            DiscountPrice.HasValue &&
            DiscountPrice.Value > 0 &&
            DiscountPrice.Value < Price
                ? DiscountPrice.Value
                : Price;

        public string? Value { get; set; }

        public byte StockMode { get; set; }
        public int StockQuantity { get; set; }

        public bool IsDefault { get; set; }

        public int SortOrder { get; set; }

        public int AvailableStock { get; set; }

        /// <summary>True when this SKU's stock mode is Unlimited (an inventory policy, not a number).</summary>
        public bool IsUnlimitedStock { get; set; }

        /// <summary>Set from the owning product; the override applies to every SKU beneath it.</summary>
        public bool ForceOutOfStock { get; set; }
    }
}
