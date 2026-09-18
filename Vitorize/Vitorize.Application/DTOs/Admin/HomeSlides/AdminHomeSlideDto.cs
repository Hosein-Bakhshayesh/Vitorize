namespace Vitorize.Application.DTOs.Admin.HomeSlides
{
    public class AdminHomeSlideDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? ImagePath { get; set; }
        public string? MobileImagePath { get; set; }
        public string? AltText { get; set; }
        public string? MobileAltText { get; set; }
        public string? LinkUrl { get; set; }
        public string? LinkText { get; set; }
        public string Placement { get; set; } = "home-bottom";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateHomeSlideRequestDto
    {
        public string Title { get; set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? ImagePath { get; set; }
        public string? MobileImagePath { get; set; }
        public string? AltText { get; set; }
        public string? MobileAltText { get; set; }
        public string? LinkUrl { get; set; }
        public string? LinkText { get; set; }
        public string Placement { get; set; } = "home-bottom";
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
    }

    public class UpdateHomeSlideRequestDto : CreateHomeSlideRequestDto
    {
    }
}
