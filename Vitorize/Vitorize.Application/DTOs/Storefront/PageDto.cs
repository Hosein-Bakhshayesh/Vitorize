namespace Vitorize.Application.DTOs.Storefront
{
    public class PageDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string Slug { get; set; } = null!;

        public string ContentHtml { get; set; } = null!;

        public string? SeoTitle { get; set; }

        public string? SeoDescription { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
