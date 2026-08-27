using System.ComponentModel.DataAnnotations;

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

        /// <summary>One or more product types. Repeated query parameters are supported.</summary>
        public List<byte>? ProductTypes { get; set; }

        public byte? DeliveryType { get; set; }

        /// <summary>Minimum effective discount percentage (0-100).</summary>
        [Range(typeof(decimal), "0", "100")]
        public decimal? MinDiscountPercent { get; set; }

        /// <summary>
        /// newest | cheapest | expensive | discount | default (SortOrder)
        /// </summary>
        public string? Sort { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}
