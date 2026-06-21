namespace Vitorize.Application.DTOs.Storefront
{
    public class BlogPostDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string? Summary { get; set; }

        public string ContentHtml { get; set; } = null!;

        public string? CoverImagePath { get; set; }

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}