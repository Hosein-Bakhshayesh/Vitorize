using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class BlogPost
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Summary { get; set; }

    public string ContentHtml { get; set; } = null!;

    public string? CoverImagePath { get; set; }

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? FocusKeyword { get; set; }

    public string? CoverImageAltText { get; set; }

    public bool IsPublished { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
