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

        /// <summary>True for About/Terms/Privacy/Contact, which are canonical at their short route.</summary>
        public bool IsSystem { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
