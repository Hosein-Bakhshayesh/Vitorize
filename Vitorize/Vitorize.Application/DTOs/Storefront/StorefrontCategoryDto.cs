namespace Vitorize.Application.DTOs.Storefront
{
    public class StorefrontCategoryDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? Icon { get; set; }

        public string? ImagePath { get; set; }
    }
}