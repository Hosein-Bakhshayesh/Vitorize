using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Categories
{
    public class AdminCategoryModel
    {
        public Guid Id { get; set; }
        public Guid? ParentId { get; set; }
        public string? ParentTitle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        public string? Icon { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public string? SeoTitle { get; set; }
        public string? SeoDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int ProductCount { get; set; }
        public int ChildrenCount { get; set; }
    }

    public class AdminCategoryInputModel
    {
        public Guid? Id { get; set; }
        public Guid? ParentId { get; set; }

        [Required(ErrorMessage = "عنوان دسته‌بندی الزامی است.")]
        [MaxLength(200, ErrorMessage = "عنوان دسته‌بندی نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسلاگ دسته‌بندی الزامی است.")]
        [MaxLength(250, ErrorMessage = "اسلاگ نمی‌تواند بیشتر از ۲۵۰ کاراکتر باشد.")]
        public string Slug { get; set; } = string.Empty;

        public string? Description { get; set; }
        public string? ImagePath { get; set; }
        [MaxLength(100)] public string? Icon { get; set; }
        [Range(0, 100000, ErrorMessage = "ترتیب نمایش معتبر نیست.")]
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        [MaxLength(250)] public string? SeoTitle { get; set; }
        [MaxLength(500)] public string? SeoDescription { get; set; }
    }
}
