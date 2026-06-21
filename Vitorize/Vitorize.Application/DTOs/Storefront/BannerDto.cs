namespace Vitorize.Application.DTOs.Storefront
{
    public class BannerDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = null!;

        public string ImagePath { get; set; } = null!;

        public string? MobileImagePath { get; set; }

        public string? LinkUrl { get; set; }

        public string Position { get; set; } = null!;

        public int SortOrder { get; set; }
    }
}