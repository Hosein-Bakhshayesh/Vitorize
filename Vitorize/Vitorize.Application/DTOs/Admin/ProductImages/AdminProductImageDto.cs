namespace Vitorize.Application.DTOs.Admin.ProductImages
{
    public class AdminProductImageDto
    {
        public Guid Id { get; set; }

        public Guid ProductId { get; set; }

        public string ImagePath { get; set; } = string.Empty;

        public string? AltText { get; set; }

        public int SortOrder { get; set; }

        public bool IsThumbnail { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}