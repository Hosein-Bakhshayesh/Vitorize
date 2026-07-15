namespace Vitorize.Application.DTOs.Storefront
{
    public class StorefrontBlogPostDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? Summary { get; set; }

        public string? CoverImagePath { get; set; }

        public string? CoverImageAltText { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
