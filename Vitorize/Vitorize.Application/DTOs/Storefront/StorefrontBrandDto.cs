namespace Vitorize.Application.DTOs.Storefront
{
    public class StorefrontBrandDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? ImagePath { get; set; }
        public string? ImageAltText { get; set; }
    }
}
