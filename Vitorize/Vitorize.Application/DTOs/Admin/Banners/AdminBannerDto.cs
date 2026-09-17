namespace Vitorize.Application.DTOs.Admin.Banners
{
    public class AdminBannerDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string? MobileImagePath { get; set; }

        /// <summary>Alt text for the desktop artwork. The column and the storefront have always had
        /// it; only this layer never carried it, so every banner fell back to its title.</summary>
        public string? AltText { get; set; }
        public string? MobileAltText { get; set; }

        public string? LinkUrl { get; set; }
        public string Position { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
