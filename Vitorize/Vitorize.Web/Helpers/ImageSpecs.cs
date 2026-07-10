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

        // بخش‌های دیگر پنل (برای استفاده در F5 — آپلود محصول/دسته/برند/بنر)
        public static readonly ImageSpec ProductThumbnail = new("۱۲۰۰×۱۲۰۰", "۱:۱", "WebP", "۲ مگابایت");
        public static readonly ImageSpec ProductGallery = new("۱۶۰۰×۱۶۰۰", "۱:۱", "WebP", "۲ مگابایت");
        public static readonly ImageSpec Brand = new("۴۰۰×۲۰۰", "۲:۱", "PNG شفاف", "۵۱۲ کیلوبایت");
        public static readonly ImageSpec Category = new("۸۰۰×۸۰۰", "۱:۱", "WebP", "۱ مگابایت");
        public static readonly ImageSpec BannerDesktop = new("۱۹۲۰×۶۴۰", "۳:۱", "WebP", "۲ مگابایت");
        public static readonly ImageSpec BannerMobile = new("۸۰۰×۱۰۰۰", "۴:۵", "WebP", "۱ مگابایت");
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
        };

        public static ImageSpec? ForKey(string? key) =>
            !string.IsNullOrWhiteSpace(key) && ByKey.TryGetValue(key, out var spec) ? spec : null;
    }
}
