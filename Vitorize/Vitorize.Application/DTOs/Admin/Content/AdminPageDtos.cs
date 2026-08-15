namespace Vitorize.Application.DTOs.Admin.Content
{
    public class AdminPageListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public bool IsSystem { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminPageDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string ContentHtml { get; set; } = string.Empty;
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public bool IsSystem { get; set; }
        public bool IsPublished { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Administrator-supplied page content. <c>IsSystem</c> is deliberately absent: system identity
    /// is decided by the seed/service, never by the client.
    /// </summary>
    public class CreatePageRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? ContentHtml { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public bool IsPublished { get; set; }
    }

    /// <summary>
    /// Update payload. For a system page the submitted slug is ignored and the stored slug is kept.
    /// </summary>
    public class UpdatePageRequestDto : CreatePageRequestDto
    {
    }
}
