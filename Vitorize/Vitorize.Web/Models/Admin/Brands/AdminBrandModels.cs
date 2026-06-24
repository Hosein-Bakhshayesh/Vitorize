using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Brands
{
    public class AdminBrandModel
    {
        public Guid Id { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Slug { get; set; } = string.Empty;

        public string? ImagePath { get; set; }

        public bool IsActive { get; set; }
    }

    public class AdminBrandInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "عنوان برند الزامی است.")]
        [MaxLength(200, ErrorMessage = "عنوان برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "اسلاگ برند الزامی است.")]
        [MaxLength(200, ErrorMessage = "اسلاگ برند نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "مسیر تصویر نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
        public string? ImagePath { get; set; }

        public bool IsActive { get; set; } = true;
    }
}