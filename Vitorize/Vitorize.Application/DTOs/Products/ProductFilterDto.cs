namespace Vitorize.Application.DTOs.Products
{
    public class ProductFilterDto
    {
        public string? Search { get; set; }

        public Guid? CategoryId { get; set; }

        public Guid? BrandId { get; set; }

        public bool? IsFeatured { get; set; }

        public decimal? MinPrice { get; set; }

        public decimal? MaxPrice { get; set; }

        public bool? HasDiscount { get; set; }

        public bool? InStock { get; set; }

        /// <summary>
        /// newest | cheapest | expensive | discount | default (SortOrder)
        /// </summary>
        public string? Sort { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
