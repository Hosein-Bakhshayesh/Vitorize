namespace Vitorize.Application.DTOs.Storefront
{
    /// <summary>One slide of a homepage slideshow, middle band or foot.</summary>
    public class HomeSlideDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Subtitle { get; set; }
        public string? ImagePath { get; set; }
        public string? MobileImagePath { get; set; }
        public string? AltText { get; set; }
        public string? MobileAltText { get; set; }
        public string? LinkUrl { get; set; }
        public string? LinkText { get; set; }
        public string Placement { get; set; } = "home-bottom";
        public int SortOrder { get; set; }
    }
}