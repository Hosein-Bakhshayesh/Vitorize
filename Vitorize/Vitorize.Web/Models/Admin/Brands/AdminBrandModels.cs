using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Brands
{
    public class AdminBrandModel
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
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ProductCount { get; set; }
    }

    public class AdminBrandInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "عنوان برند الزامی است.")]
        [MaxLength(150, ErrorMessage = "عنوان برند نمی‌تواند بیشتر از ۱۵۰ کاراکتر باشد.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسلاگ برند الزامی است.")]
        [MaxLength(200, ErrorMessage = "اسلاگ برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }
        [MaxLength(250)] public string? ImageAltText { get; set; }
        [MaxLength(2000)] public string? Description { get; set; }
        [MaxLength(250)] public string? SeoTitle { get; set; }
        [MaxLength(500)] public string? SeoDescription { get; set; }
        [MaxLength(200)] public string? FocusKeyword { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
