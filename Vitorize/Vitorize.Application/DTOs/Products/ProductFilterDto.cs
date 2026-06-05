namespace Vitorize.Application.DTOs.Products
{
    public class ProductFilterDto
    {
        public string? Search { get; set; }

        public Guid? CategoryId { get; set; }

        public Guid? BrandId { get; set; }

        public bool? IsFeatured { get; set; }

        public int Page { get; set; } = 1;

        public int PageSize { get; set; } = 20;
    }
}