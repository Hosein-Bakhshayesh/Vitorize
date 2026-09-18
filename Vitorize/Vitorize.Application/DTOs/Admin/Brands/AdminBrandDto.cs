namespace Vitorize.Application.DTOs.Admin.Brands
{
    public class AdminBrandDto
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }
        public string? ImageAltText { get; set; }
        public string? Description { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }

        public int SortOrder { get; set; }


        public bool IsActive { get; set; }

        public DateTime CreatedAt { get; set; }

        /// <summary>Active products carrying this brand. The list column existed long before
        /// anything filled it, so every brand read as "بدون محصول" whatever the catalogue held.</summary>
        public int ProductCount { get; set; }
    }
}
