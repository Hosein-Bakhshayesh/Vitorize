using System;

namespace Vitorize.Domain.Entities;

/// <summary>
/// One slide of a homepage slideshow. Unlike a banner it carries copy as well as artwork, which is
/// why it is its own entity rather than more columns on <see cref="Banner"/>; <see cref="Placement"/>
/// then says which of the page's two slideshows it belongs to.
/// </summary>
public partial class HomeSlide
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Subtitle { get; set; }

    public string? ImagePath { get; set; }

    public string? MobileImagePath { get; set; }

    public string? AltText { get; set; }

    public string? MobileAltText { get; set; }

    public string? LinkUrl { get; set; }

    /// <summary>Label of the slide's button, for example "برو اینستاگرام".</summary>
    public string? LinkText { get; set; }

    /// <summary>One of <c>Vitorize.Shared.Storefront.HomeSlidePlacements</c>.</summary>
    public string Placement { get; set; } = "home-bottom";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; }

    public DateTime? StartsAt { get; set; }

    public DateTime? EndsAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
