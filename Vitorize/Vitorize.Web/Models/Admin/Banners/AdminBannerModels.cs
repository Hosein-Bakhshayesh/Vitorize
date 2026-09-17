using System.ComponentModel.DataAnnotations;

namespace Vitorize.Web.Models.Admin.Banners
{
    public class AdminBannerModel
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string? MobileImagePath { get; set; }
        public string? AltText { get; set; }
        public string? MobileAltText { get; set; }
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

        [MaxLength(250, ErrorMessage = "متن جایگزین نمی‌تواند بیشتر از ۲۵۰ کاراکتر باشد.")]
        public string? AltText { get; set; }

        [MaxLength(250, ErrorMessage = "متن جایگزین موبایل نمی‌تواند بیشتر از ۲۵۰ کاراکتر باشد.")]
        public string? MobileAltText { get; set; }

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

    /// <summary>
    /// One place a banner can appear, as the administrator thinks of it.
    /// <para>
    /// The homepage top is a mosaic of four fixed tiles of different shapes, not a slider, and which
    /// tile a banner lands in comes from its sort order. Asking for a position and a bare number
    /// separately meant nobody could tell where a banner would end up without opening the site, so the
    /// two are chosen together here and stored exactly as before.
    /// </para>
    /// </summary>
    public sealed class AdminBannerSlot
    {
        public AdminBannerSlot(string key, string position, int? fixedSortOrder, string title, string shape)
        {
            Key = key;
            Position = position;
            FixedSortOrder = fixedSortOrder;
            Title = title;
            Shape = shape;
        }

        public string Key { get; }
        public string Position { get; }

        /// <summary>The sort order this slot pins, or null where the order is the slide sequence.</summary>
        public int? FixedSortOrder { get; }

        public string Title { get; }

        /// <summary>Short description of the frame, so the required proportions are obvious.</summary>
        public string Shape { get; }

        public bool IsHeroTile => FixedSortOrder.HasValue;

        public static readonly AdminBannerSlot[] All =
        {
            new("hero-1", "home-hero", 0, "بالای صفحه — کاشی ۱", "مستطیل افقی، ردیف بالا"),
            new("hero-2", "home-hero", 1, "بالای صفحه — کاشی ۲", "تقریباً مربع، ردیف بالا"),
            new("hero-3", "home-hero", 2, "بالای صفحه — کاشی ۳", "مستطیل پهن، ردیف پایین"),
            new("hero-4", "home-hero", 3, "بالای صفحه — کاشی ۴", "مستطیل عمودی باریک، ردیف پایین"),
            new("slider", "home-secondary", null, "بنر میانی صفحه — اسلاید", "بنر پهن ۱۶:۹، چند اسلاید")
        };

        public const int HeroTileCount = 4;

        public static AdminBannerSlot? Find(string? key) =>
            All.FirstOrDefault(x => x.Key == key);

        /// <summary>
        /// The slot a stored banner occupies, or null when a hero banner sits past the fourth tile —
        /// the homepage reads only four, so such a row is configured but never drawn.
        /// </summary>
        public static AdminBannerSlot? Resolve(string position, int sortOrder) =>
            All.FirstOrDefault(x =>
                x.Position == position &&
                (x.FixedSortOrder is null || x.FixedSortOrder == sortOrder));
    }
}
