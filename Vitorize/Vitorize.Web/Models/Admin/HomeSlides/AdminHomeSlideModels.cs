using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.HomeSlides
{
    public class AdminHomeSlideModel
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
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AdminHomeSlideInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "عنوان اسلاید الزامی است.")]
        [MaxLength(200, ErrorMessage = "عنوان نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "متن نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
        public string? Subtitle { get; set; }

        [MaxLength(500, ErrorMessage = "مسیر تصویر معتبر نیست.")]
        public string? ImagePath { get; set; }

        [MaxLength(500, ErrorMessage = "مسیر تصویر موبایل معتبر نیست.")]
        public string? MobileImagePath { get; set; }

        [MaxLength(250, ErrorMessage = "متن جایگزین نمی‌تواند بیشتر از ۲۵۰ کاراکتر باشد.")]
        public string? AltText { get; set; }

        [MaxLength(250, ErrorMessage = "متن جایگزین موبایل نمی‌تواند بیشتر از ۲۵۰ کاراکتر باشد.")]
        public string? MobileAltText { get; set; }

        [MaxLength(500, ErrorMessage = "لینک نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
        public string? LinkUrl { get; set; }

        [MaxLength(100, ErrorMessage = "متن دکمه نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
        public string? LinkText { get; set; }

        [Range(0, 999999, ErrorMessage = "ترتیب نمایش معتبر نیست.")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
    }
}
