using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Banners
{
    public class AdminBannerModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string? MobileImagePath { get; set; }
        public string? LinkUrl { get; set; }
        public string Position { get; set; } = string.Empty;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AdminBannerInputModel
    {
        public Guid? Id { get; set; }

        [Required(ErrorMessage = "عنوان بنر الزامی است.")]
        [MaxLength(200, ErrorMessage = "عنوان بنر نمی‌تواند بیشتر از ۲۰۰ کاراکتر باشد.")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "تصویر اصلی بنر الزامی است.")]
        [MaxLength(500, ErrorMessage = "مسیر تصویر معتبر نیست.")]
        public string ImagePath { get; set; } = string.Empty;

        [MaxLength(500, ErrorMessage = "مسیر تصویر موبایل معتبر نیست.")]
        public string? MobileImagePath { get; set; }

        [MaxLength(500, ErrorMessage = "لینک بنر نمی‌تواند بیشتر از ۵۰۰ کاراکتر باشد.")]
        public string? LinkUrl { get; set; }

        [Required(ErrorMessage = "جایگاه نمایش الزامی است.")]
        [MaxLength(100, ErrorMessage = "جایگاه نمایش نمی‌تواند بیشتر از ۱۰۰ کاراکتر باشد.")]
        public string Position { get; set; } = "home-hero";

        [Range(0, 999999, ErrorMessage = "ترتیب نمایش معتبر نیست.")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
    }

    public class AdminBannerPositionOption
    {
        public AdminBannerPositionOption(string value, string title)
        {
            Value = value;
            Title = title;
        }

        public string Value { get; }
        public string Title { get; }
    }
}
