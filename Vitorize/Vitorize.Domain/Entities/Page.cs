using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Page
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string ContentHtml { get; set; } = null!;

    public string? SeoTitle { get; set; }

    public string? SeoDescription { get; set; }

    public string? FocusKeyword { get; set; }

    public bool IsPublished { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
