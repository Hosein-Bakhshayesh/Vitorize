namespace Vitorize.Application.DTOs.Products
{
    public class ProductLookupDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public string? ImageAltText { get; set; }
        public string? Description { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
