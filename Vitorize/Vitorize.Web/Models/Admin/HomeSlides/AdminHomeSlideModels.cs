using System.ComponentModel.DataAnnotations;
using Vitorize.Shared.Storefront;

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
        public string Placement { get; set; } = HomeSlidePlacements.Bottom;
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>
    /// Which fields are required depends on where the slide goes, so the rules live in
    /// <see cref="Validate"/> rather than in attributes that cannot see the placement.
    /// </summary>
    public class AdminHomeSlideInputModel : IValidatableObject
    {
        public Guid? Id { get; set; }

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

        public string Placement { get; set; } = HomeSlidePlacements.Bottom;

        [Range(0, 999999, ErrorMessage = "ترتیب نمایش معتبر نیست.")]
        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime? StartsAt { get; set; }
        public DateTime? EndsAt { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            // The foot slideshow prints the heading beside the artwork; the middle band is usually
            // a full-bleed image whose wording is already part of the picture. Mirrors the server.
            if (string.IsNullOrWhiteSpace(Title) && Placement != HomeSlidePlacements.Middle)
                yield return new ValidationResult("عنوان اسلاید الزامی است.", new[] { nameof(Title) });

            if (string.IsNullOrWhiteSpace(ImagePath) && Placement == HomeSlidePlacements.Middle)
                yield return new ValidationResult("برای بنر میانی انتخاب تصویر الزامی است.", new[] { nameof(ImagePath) });

            if (!string.IsNullOrWhiteSpace(LinkText) && string.IsNullOrWhiteSpace(LinkUrl))
                yield return new ValidationResult("برای متن دکمه باید لینک مقصد هم وارد شود.", new[] { nameof(LinkUrl) });

            if (StartsAt.HasValue && EndsAt.HasValue && StartsAt > EndsAt)
                yield return new ValidationResult("تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد.", new[] { nameof(EndsAt) });
        }
    }
}
