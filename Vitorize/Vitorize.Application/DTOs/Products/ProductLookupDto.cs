namespace Vitorize.Application.DTOs.Products
{
    public class ProductLookupDto
    {
        public Guid Id { get; set; }

        /// <summary>
        /// Parent category for storefront navigation. Null means that this is a top-level category.
        /// Brands always leave this value null.
        /// </summary>
        public Guid? ParentId { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        /// <summary>
        /// Configured icon key. Categories may be presented as an icon instead of an image; without
        /// this the storefront had no way to know and fell back to a generic glyph.
        /// </summary>
        public string? Icon { get; set; }

        public string? ImagePath { get; set; }

        public string? ImageAltText { get; set; }
        public string? Description { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
