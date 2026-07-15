using System;
using System.Collections.Generic;

namespace Vitorize.Domain.Entities;

public partial class Banner
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string ImagePath { get; set; } = null!;

    public string? MobileImagePath { get; set; }

    public string? AltText { get; set; }

    public string? MobileAltText { get; set; }

    public string? LinkUrl { get; set; }

    public string Position { get; set; } = null!;

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public DateTime CreatedAt { get; set; }
}
