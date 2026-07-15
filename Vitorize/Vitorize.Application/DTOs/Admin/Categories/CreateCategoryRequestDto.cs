namespace Vitorize.Application.DTOs.Admin.Categories
{
    public class CreateCategoryRequestDto
    {
        public Guid? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string? ImageAltText { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public string? FocusKeyword { get; set; }
    }
}
