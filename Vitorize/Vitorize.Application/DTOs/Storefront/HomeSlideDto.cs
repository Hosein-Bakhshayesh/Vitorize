namespace Vitorize.Application.DTOs.Storefront
{
    /// <summary>One slide of the promotional block at the foot of the homepage.</summary>
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
        public int SortOrder { get; set; }
    }
}