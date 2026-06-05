namespace Vitorize.Application.DTOs.Admin.Categories
{
    public class AdminCategoryDto
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
    }
}