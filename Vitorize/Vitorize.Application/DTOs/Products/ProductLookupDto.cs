namespace Vitorize.Application.DTOs.Products
{
    public class ProductLookupDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }
    }
}