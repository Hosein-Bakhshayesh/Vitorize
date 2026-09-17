namespace Vitorize.Web.Helpers
{
    /// <summary>راهنمای ابعاد پیشنهادی تصویر برای هر بخش آپلود در پنل مدیریت.</summary>
    public sealed record ImageSpec(string Dimensions, string Ratio, string Format, string MaxSize, string? Note = null);

    /// <summary>
    /// رجیستری مشخصات پیشنهادی تصاویر. با نام اسپک یا کلید تنظیمات قابل واکشی است تا
    /// مدیر همیشه بداند چه ابعادی آپلود کند (بخش ۴ سند الزامات).
    /// </summary>
    public static class ImageSpecs
    {
        public static readonly ImageSpec Logo = new("۵۱۲×۱۶۰", "آزاد (افقی)", "SVG یا PNG شفاف", "۵۱۲ کیلوبایت", "پس‌زمینه شفاف");
        public static readonly ImageSpec LogoDark = new("۵۱۲×۱۶۰", "آزاد (افقی)", "SVG یا PNG شفاف", "۵۱۲ کیلوبایت", "مناسب تم تیره");
        public static readonly ImageSpec LogoSmall = new("۱۲۸×۱۲۸", "۱:۱", "SVG یا PNG شفاف", "۲۵۶ کیلوبایت");
        public static readonly ImageSpec Favicon = new("۶۴×۶۴", "۱:۱", "PNG یا ICO", "۱۲۸ کیلوبایت");
        public static readonly ImageSpec AppleTouchIcon = new("۱۸۰×۱۸۰", "۱:۱", "PNG", "۲۵۶ کیلوبایت");
        public static readonly ImageSpec OgImage = new("۱۲۰۰×۶۳۰", "۱.۹۱:۱", "JPG یا PNG", "۱ مگابایت", "پیش‌نمایش اشتراک‌گذاری");
        public static readonly ImageSpec HeroBackground = new("۱۹۲۰×۱۰۸۰", "۱۶:۹", "WebP یا JPG", "۲ مگابایت");
        public static readonly ImageSpec Illustration = new("۵۱۲×۵۱۲", "۱:۱", "SVG یا PNG شفاف", "۵۱۲ کیلوبایت", "ماسکات / تصویر حالت");
        public static readonly ImageSpec LoadingMedia = new("۲۴۰×۲۴۰", "۱:۱", "PNG، WebP یا GIF", "۵۱۲ کیلوبایت", "خالی = لودر پیش‌فرض؛ فایل سبک نگه دارید");

        // بخش‌های دیگر پنل (برای استفاده در F5 — آپلود محصول/دسته/برند/بنر)
        public static readonly ImageSpec ProductThumbnail = new("۱۲۰۰×۱۲۰۰", "۱:۱", "WebP", "۲ مگابایت");
        public static readonly ImageSpec ProductGallery = new("۱۶۰۰×۱۶۰۰", "۱:۱", "WebP", "۲ مگابایت");
        public static readonly ImageSpec Brand = new("۴۰۰×۲۰۰", "۲:۱", "PNG شفاف", "۵۱۲ کیلوبایت");
        public static readonly ImageSpec Category = new("۸۰۰×۸۰۰", "۱:۱", "WebP", "۱ مگابایت");
        // Measured from the rendered homepage, one per slot. A single pair of numbers used to be shown
        // for every banner, and it matched none of them: the top of the page is four tiles of four
        // different shapes and the middle banner is 16:9, so an image built to the old 2:1 guidance was
        // cropped on upload. The mobile layout keeps these proportions to within a few percent, so one
        // file per slot serves both as long as the subject sits near the middle.
        public static readonly ImageSpec BannerHeroTile1 = new("۱۳۱۰×۸۲۰", "۱.۶:۱", "WebP", "۵۰۰ کیلوبایت", "کاشی بزرگ ردیف بالا");
        public static readonly ImageSpec BannerHeroTile2 = new("۸۰۰×۸۳۰", "۱:۱", "WebP", "۵۰۰ کیلوبایت", "کاشی تقریباً مربع ردیف بالا");
        public static readonly ImageSpec BannerHeroTile3 = new("۱۶۰۰×۸۳۰", "۱.۹:۱", "WebP", "۵۰۰ کیلوبایت", "کاشی پهن ردیف پایین");
        public static readonly ImageSpec BannerHeroTile4 = new("۵۲۰×۸۳۰", "۰.۶:۱", "WebP", "۵۰۰ کیلوبایت", "قاب عمودی است؛ تصویر افقی در آن به‌شدت برش می‌خورد");
        public static readonly ImageSpec HomeSlide = new("۲۴۰۰×۶۷۵", "۳.۵:۱", "WebP", "۱ مگابایت", "قاب بسیار پهن؛ متن روی تصویر می‌نشیند، سمت راست را ساده نگه دارید");
        public static readonly ImageSpec BannerSlider = new("۱۹۲۰×۱۰۸۰", "۱۶:۹", "WebP", "۱ مگابایت", "بنر پهن میانی صفحه");

        /// <summary>Guidance for a banner slot, keyed by <c>AdminBannerSlot.Key</c>.</summary>
        public static ImageSpec? ForBannerSlot(string? slotKey) => slotKey switch
        {
            "hero-1" => BannerHeroTile1,
            "hero-2" => BannerHeroTile2,
            "hero-3" => BannerHeroTile3,
            "hero-4" => BannerHeroTile4,
            "slider" => BannerSlider,
            _ => null
        };
        public static readonly ImageSpec BlogCover = new("۱۲۰۰×۶۳۰", "۱.۹۱:۱", "WebP یا JPG", "۱ مگابایت");
        public static readonly ImageSpec Avatar = new("۴۰۰×۴۰۰", "۱:۱", "PNG یا JPG", "۵۱۲ کیلوبایت");
        public static readonly ImageSpec Verification = new("۱۶۰۰×۱۲۰۰", "۴:۳", "JPG", "۲ مگابایت", "خوانا و بدون تاری");

        private static readonly Dictionary<string, ImageSpec> ByKey = new(StringComparer.OrdinalIgnoreCase)
        {
            ["LogoPath"] = Logo,
            ["HeaderLogoPath"] = Logo,
            ["FooterLogoPath"] = Logo,
            ["SiteLogoPath"] = Logo,
            ["LogoDarkPath"] = LogoDark,
            ["LogoSmallPath"] = LogoSmall,
            ["FaviconPath"] = Favicon,
            ["AppleTouchIconPath"] = AppleTouchIcon,
            ["OgImagePath"] = OgImage,
            ["TwitterImagePath"] = OgImage,
            ["SocialPreviewImagePath"] = OgImage,
            ["HeroBackgroundPath"] = HeroBackground,
            ["Error404IllustrationPath"] = Illustration,
            ["Error500IllustrationPath"] = Illustration,
            ["MaintenanceIllustrationPath"] = Illustration,
            ["EmptyStateIllustrationPath"] = Illustration,
            ["LoadingMediaPath"] = LoadingMedia,
        };

        public static ImageSpec? ForKey(string? key) =>
            !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key, out var spec) ? spec : null;
    }
}
