using System.Collections.Concurrent;
using System.Text.Json;

namespace Vitorize.Web.Services.UI
{
    /// <summary>یک بلوک تکرارشونده‌ی صفحه اول (نشان اعتماد / ویژگی): آیکون، عنوان، متن.</summary>
    public sealed record HomeBlockItem(string Icon, string Title, string Text);

    /// <summary>
    /// برندینگ قابل‌تنظیم فروشگاه (نام، شعار، متن‌های Hero و فوتر و ...) از تنظیمات عمومی API.
    /// نتیجه در یک کش استاتیک با TTL کوتاه نگه داشته می‌شود تا هر مدار Blazor درخواست جدا نزند.
    /// در صورت در دسترس نبودن API، مقادیر پیش‌فرض برگردانده می‌شوند.
    /// </summary>
    public class StoreBrandingService
    {
        private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(2);

        private static ConcurrentDictionary<string, string>? _cache;
        private static DateTime _cacheLoadedAt = DateTime.MinValue;
        private static readonly SemaphoreSlim Gate = new(1, 1);

        private readonly ApiClient _api;

        public StoreBrandingService(ApiClient api)
        {
            _api = api;
        }

        public async Task<StoreBranding> GetAsync()
        {
            var values = await GetValuesAsync();

            return new StoreBranding(values);
        }

        /// <summary>پس از ذخیره‌ی تنظیمات در پنل مدیریت، کش را نامعتبر می‌کند.</summary>
        public static void Invalidate() => _cacheLoadedAt = DateTime.MinValue;

        private async Task<ConcurrentDictionary<string, string>> GetValuesAsync()
        {
            if (_cache != null && DateTime.UtcNow - _cacheLoadedAt < CacheTtl)
                return _cache;

            await Gate.WaitAsync();

            try
            {
                if (_cache != null && DateTime.UtcNow - _cacheLoadedAt < CacheTtl)
                    return _cache;

                var result = await _api.GetAsync<List<PublicSettingModel>>("settings/public");

                if (result.IsSuccess && result.Data is not null)
                {
                    var map = new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                    foreach (var item in result.Data)
                    {
                        if (!string.IsNullOrWhiteSpace(item.Key))
                            map[item.Key] = item.Value ?? string.Empty;
                    }

                    _cache = map;
                    _cacheLoadedAt = DateTime.UtcNow;
                }

                return _cache ?? new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return _cache ?? new ConcurrentDictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            finally
            {
                Gate.Release();
            }
        }

        private sealed class PublicSettingModel
        {
            public string Key { get; set; } = string.Empty;
            public string? Value { get; set; }
        }
    }

    /// <summary>مقادیر برندینگ با پیش‌فرض‌های امن؛ مقدار خالی در تنظیمات یعنی استفاده از پیش‌فرض.</summary>
    public class StoreBranding
    {
        private readonly IReadOnlyDictionary<string, string> _values;

        public StoreBranding(IReadOnlyDictionary<string, string> values) => _values = values;

        // ── General / branding ──
        public string SiteName => Get("SiteName", "ویتورایز");
        public string SiteDescription => Get("SiteDescription", "فروشگاه گیفت کارت و سرویس‌های دیجیتال");
        public string SiteTagline => Get("SiteTagline", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین");
        public string SiteLogoPath => Get("SiteLogoPath", "");
        public bool MaintenanceMode => GetBool("MaintenanceMode");
        public string MaintenanceMessage => Get("MaintenanceMessage", "به‌زودی با نسخه‌ای بهتر برمی‌گردیم.");

        // ── Logos & images (empty ⇒ built-in default) ──
        public string LogoPath => Get("LogoPath", "");
        public string LogoDarkPath => Get("LogoDarkPath", "");
        public string LogoSmallPath => Get("LogoSmallPath", "");
        public string HeaderLogoPath => FirstNonEmpty("HeaderLogoPath", "LogoPath", "SiteLogoPath");
        public string FooterLogoPath => FirstNonEmpty("FooterLogoPath", "LogoPath", "SiteLogoPath");
        public string FaviconPath => Get("FaviconPath", "");
        public string AppleTouchIconPath => Get("AppleTouchIconPath", "");
        public string OgImagePath => FirstNonEmpty("OgImagePath", "SocialPreviewImagePath");
        public string TwitterImagePath => FirstNonEmpty("TwitterImagePath", "OgImagePath", "SocialPreviewImagePath");
        public string HeroBackgroundPath => Get("HeroBackgroundPath", "");
        public string Error404IllustrationPath => FirstNonEmpty("Error404IllustrationPath", "EmptyStateIllustrationPath");
        public string Error500IllustrationPath => FirstNonEmpty("Error500IllustrationPath", "EmptyStateIllustrationPath");
        public string MaintenanceIllustrationPath => FirstNonEmpty("MaintenanceIllustrationPath", "EmptyStateIllustrationPath");
        public string EmptyStateIllustrationPath => Get("EmptyStateIllustrationPath", "");

        // ── SEO ──
        public string MetaTitle => Get("MetaTitle", "");
        public string MetaDescription => FirstNonEmpty("MetaDescription", "SiteDescription");
        public string MetaKeywords => Get("MetaKeywords", "");
        public string GoogleAnalyticsId => Get("GoogleAnalyticsId", "");

        // ── Homepage / hero ──
        public string HeroKicker => Get("HeroKicker", "ویتورایز · بازارگاه دیجیتال");
        public string HeroTitle => Get("HeroTitle", "دنیای بازی و دیجیتال در دستان تو");
        public string HeroSubtitle => Get("HeroSubtitle", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.");
        public string HeroCtaText => Get("HeroCtaText", "ورود به فروشگاه");
        public string HeroCtaUrl => Get("HeroCtaUrl", "/shop");
        public string HeroSecondaryCtaText => Get("HeroSecondaryCtaText", "دسته‌بندی‌ها");
        public string HeroSecondaryCtaUrl => Get("HeroSecondaryCtaUrl", "/categories");
        public string NewsletterTitle => Get("NewsletterTitle", "از جدیدترین‌ها باخبر شو");
        public string NewsletterSubtitle => Get("NewsletterSubtitle", "با عضویت در خبرنامه، از تخفیف‌ها و محصولات تازه زودتر از همه مطلع شو.");
        public string NewsletterCtaText => Get("NewsletterCtaText", "عضویت");
        public string NewsletterPlaceholder => Get("NewsletterPlaceholder", "ایمیل خود را وارد کنید");

        // ── About / trust ──
        public string AboutTitle => Get("AboutTitle", "درباره ویتورایز");
        public string AboutText => Get("AboutText", "");

        public string HomeFeaturesKicker => Get("HomeFeaturesKicker", "چرا ویتورایز؟");
        public string HomeFeaturesTitle => Get("HomeFeaturesTitle", "خرید دیجیتال، ساده و مطمئن");

        private const string DefaultTrustJson =
            "[{\"icon\":\"shield-check\",\"title\":\"تضمین اصالت\",\"text\":\"محصولات رسمی و اورجینال\"},{\"icon\":\"zap\",\"title\":\"تحویل آنی\",\"text\":\"سریع و بدون انتظار\"},{\"icon\":\"headphones\",\"title\":\"پشتیبانی ۲۴/۷\",\"text\":\"همیشه کنار شما\"},{\"icon\":\"lock\",\"title\":\"پرداخت امن\",\"text\":\"درگاه‌های معتبر\"}]";
        private const string DefaultFeaturesJson =
            "[{\"icon\":\"grid\",\"title\":\"انتخاب محصول\",\"text\":\"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن.\"},{\"icon\":\"credit-card\",\"title\":\"پرداخت امن\",\"text\":\"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده.\"},{\"icon\":\"zap\",\"title\":\"تحویل آنی\",\"text\":\"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود.\"}]";

        public string TrustBadgesJson => Get("TrustBadgesJson", DefaultTrustJson);
        public string HomeFeaturesJson => Get("HomeFeaturesJson", DefaultFeaturesJson);

        public IReadOnlyList<HomeBlockItem> TrustBadges => ParseBlocks(TrustBadgesJson);
        public IReadOnlyList<HomeBlockItem> HomeFeatures => ParseBlocks(HomeFeaturesJson);

        private static readonly JsonSerializerOptions BlockJsonOptions = new() { PropertyNameCaseInsensitive = true };

        private static IReadOnlyList<HomeBlockItem> ParseBlocks(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<HomeBlockItem>();
            try
            {
                var items = JsonSerializer.Deserialize<List<HomeBlockItem>>(json, BlockJsonOptions);
                return items?.Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Title)).ToList()
                       ?? (IReadOnlyList<HomeBlockItem>)Array.Empty<HomeBlockItem>();
            }
            catch
            {
                return Array.Empty<HomeBlockItem>();
            }
        }

        // ── Footer ──
        public string FooterDescription => Get("FooterDescription", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین؛ خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی.");
        public string CopyrightText => Get("CopyrightText", "تمامی حقوق برای ویتورایز محفوظ است.");
        public string FooterText => Get("FooterText", "");

        // ── Contact ──
        public string SupportPhone => Get("SupportPhone", "");
        public string SupportEmail => Get("SupportEmail", "");
        public string ContactAddress => Get("ContactAddress", "");
        public string WorkingHours => Get("WorkingHours", "");

        // ── Social ──
        public string InstagramUrl => Get("InstagramUrl", "");
        public string TelegramUrl => Get("TelegramUrl", "");
        public string WhatsAppUrl => Get("WhatsAppUrl", "");
        public string XUrl => Get("XUrl", "");
        public string LinkedInUrl => Get("LinkedInUrl", "");
        public string DiscordUrl => Get("DiscordUrl", "");
        public string YouTubeUrl => Get("YouTubeUrl", "");
        public string FacebookUrl => Get("FacebookUrl", "");

        // ── Custom scripts ──
        public string CustomHeadHtml => Get("CustomHeadHtml", "");
        public string CustomFooterHtml => Get("CustomFooterHtml", "");

        public string PageTitle(string? page = null)
        {
            var template = Get("SeoTitleTemplate", "{page} | {site}");

            if (string.IsNullOrWhiteSpace(page))
            {
                var home = MetaTitle;
                return !string.IsNullOrWhiteSpace(home) ? home : $"{SiteName} | {SiteTagline}";
            }

            return template.Replace("{page}", page).Replace("{site}", SiteName);
        }

        /// <summary>خواندن هر کلید عمومی با مقدار پیش‌فرض (برای متن‌های خطا/خالی و ...).</summary>
        public string Get(string key, string fallback) =>
            _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;

        public bool GetBool(string key, bool fallback = false) =>
            _values.TryGetValue(key, out var value) && bool.TryParse(value, out var b) ? b : fallback;

        private string FirstNonEmpty(params string[] keys)
        {
            foreach (var key in keys)
                if (_values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    return value;
            return string.Empty;
        }
    }
}
