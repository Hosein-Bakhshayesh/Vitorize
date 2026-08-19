namespace Vitorize.Application.DTOs.Admin.Content
{
    public class AdminBlogPostListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminBlogPostDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string ContentHtml { get; set; } = string.Empty;
        public string? CoverImagePath { get; set; }
        public string? CoverImageAltText { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Administrator-supplied article content. <c>PublishedAt</c> is deliberately absent: the service
    /// stamps it when a post first becomes published, so a client cannot backdate an article.
    /// </summary>
    public class CreateBlogPostRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public string? ContentHtml { get; set; }
        public string? CoverImagePath { get; set; }
        public string? CoverImageAltText { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }
        /// <summary>New articles default to draft; publishing is always an explicit choice.</summary>
        public bool IsPublished { get; set; }
    }

    public class UpdateBlogPostRequestDto : CreateBlogPostRequestDto
    {
    }
}
