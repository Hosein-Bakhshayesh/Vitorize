using System.Collections.Concurrent;

namespace Vitorize.Web.Services.UI
{
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

        public string SiteName => Get("SiteName", "ویتورایز");
        public string SiteTagline => Get("SiteTagline", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین");
        public string SiteLogoPath => Get("SiteLogoPath", "");
        public string HeroKicker => Get("HeroKicker", "ویتورایز · بازارگاه دیجیتال");
        public string HeroTitle => Get("HeroTitle", "دنیای بازی و دیجیتال در دستان تو");
        public string HeroSubtitle => Get("HeroSubtitle", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.");
        public string HeroCtaText => Get("HeroCtaText", "ورود به فروشگاه");
        public string FooterDescription => Get("FooterDescription", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین؛ خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی.");
        public string CopyrightText => Get("CopyrightText", "تمامی حقوق برای ویتورایز محفوظ است.");
        public string SupportPhone => Get("SupportPhone", "");
        public string SupportEmail => Get("SupportEmail", "");
        public string InstagramUrl => Get("InstagramUrl", "");
        public string TelegramUrl => Get("TelegramUrl", "");

        public string PageTitle(string? page = null) =>
            string.IsNullOrWhiteSpace(page)
                ? $"{SiteName} | {SiteTagline}"
                : $"{page} | {SiteName}";

        private string Get(string key, string fallback) =>
            _values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
                ? value
                : fallback;
    }
}
