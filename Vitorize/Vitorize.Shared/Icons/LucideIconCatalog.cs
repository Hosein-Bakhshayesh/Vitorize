using System.Collections.Frozen;
using System.Globalization;
using System.Text;

namespace Vitorize.Shared.Icons;

public sealed record LucideIconCategory(string Key, string PersianTitle);

public sealed record LucideIconEntry(
    string Key,
    string EnglishName,
    string PersianAliases,
    string Category,
    IReadOnlyList<string> Tags,
    int Popularity,
    string NormalizedSearchText);

public static class LucideIconCatalog
{
    public static string PackageName => LucideIconCatalogData.PackageName;
    public static string Version => LucideIconCatalogData.Version;

    public static IReadOnlyList<LucideIconCategory> Categories { get; } =
    [
        new("General", "عمومی"), new("Interface", "رابط کاربری"), new("Arrows", "جهت‌ها"),
        new("Actions", "عملیات"), new("Status", "وضعیت"), new("Alerts", "هشدارها"),
        new("Commerce", "تجارت"), new("Shopping", "خرید"), new("Payments", "پرداخت"),
        new("Wallet", "کیف پول"), new("Gaming", "بازی"), new("Devices", "دستگاه‌ها"),
        new("Cloud", "فضای ابری"), new("AI", "هوش مصنوعی"), new("Development", "توسعه"),
        new("Database", "دیتابیس"), new("Server", "سرور"), new("Network", "شبکه"),
        new("Security", "امنیت"), new("Users", "کاربران"), new("Communication", "ارتباطات"),
        new("Email", "ایمیل"), new("SMS", "پیامک"), new("Chat", "گفتگو"),
        new("Support", "پشتیبانی"), new("Tickets", "تیکت"), new("Notifications", "اعلان‌ها"),
        new("Calendar", "تقویم"), new("Time", "زمان"), new("Files", "فایل‌ها"),
        new("Folders", "پوشه‌ها"), new("Media", "رسانه"), new("Images", "تصاویر"),
        new("Analytics", "تحلیل"), new("Charts", "نمودارها"), new("Business", "کسب‌وکار"),
        new("Legal", "حقوقی"), new("Education", "آموزش"), new("Health", "سلامت"),
        new("Travel", "سفر"), new("Transport", "حمل‌ونقل"), new("Weather", "آب‌وهوا"),
        new("Social/Semantic", "اجتماعی و مفهومی"), new("Miscellaneous", "سایر")
    ];

    public static IReadOnlyDictionary<string, string> LegacyMappings { get; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["alert"] = "triangle-alert",
            ["bar-chart"] = "chart-no-axes-column",
            ["cart"] = "shopping-cart",
            ["check-circle"] = "circle-check",
            ["circle-help"] = "circle-question-mark",
            ["dashboard"] = "layout-dashboard",
            ["dots"] = "ellipsis",
            ["edit"] = "pencil",
            ["external"] = "external-link",
            ["filter"] = "funnel",
            ["grid"] = "layout-grid",
            ["home"] = "house",
            ["logout"] = "log-out",
            ["message"] = "message-circle",
            ["refresh"] = "refresh-cw",
            ["sliders"] = "sliders-horizontal",
            ["shield-lock"] = "shield-check",
            ["x-circle"] = "circle-x",
            ["activity-square"] = "square-activity",
            ["facebook"] = "users",
            ["instagram"] = "camera",
            ["linkedin"] = "briefcase-business",
            ["twitter"] = "message-circle",
            ["youtube"] = "circle-play"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, string> PersianAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["gamepad-2"] = "بازی گیم دسته بازی کنسول پلی استیشن ایکس باکس استیم کنترلر playstation xbox steam gaming controller",
            ["gamepad"] = "بازی گیم دسته بازی کنسول کنترلر",
            ["joystick"] = "بازی گیم جوی استیک کنترلر",
            ["trophy"] = "جام جایزه برنده موفقیت بازی",
            ["wallet"] = "کیف پول موجودی اعتبار",
            ["wallet-cards"] = "کیف پول کارت اعتبار پرداخت",
            ["credit-card"] = "کارت بانکی پرداخت درگاه",
            ["banknote"] = "پول اسکناس پرداخت",
            ["shopping-cart"] = "سبد خرید سفارش فروشگاه",
            ["shopping-bag"] = "خرید فروشگاه محصول",
            ["package"] = "محصول کالا بسته سفارش",
            ["receipt-text"] = "سفارش فاکتور رسید پیگیری",
            ["ticket"] = "تیکت پشتیبانی بلیت",
            ["tickets"] = "تیکت ها پشتیبانی بلیت",
            ["headphones"] = "پشتیبانی هدفون پاسخگویی",
            ["headset"] = "پشتیبانی هدست پاسخگویی",
            ["message-circle"] = "پیام چت گفتگو تیکت دیسکورد discord",
            ["messages-square"] = "پیام ها چت گفتگو دیسکورد discord",
            ["send"] = "ارسال تلگرام پیام telegram",
            ["mail"] = "ایمیل نامه پست الکترونیکی",
            ["message-square-text"] = "پیامک اس ام اس sms متن",
            ["smartphone"] = "موبایل گوشی تلفن پیامک sms",
            ["bell"] = "اعلان اطلاع رسانی نوتیفیکیشن",
            ["bell-ring"] = "اعلان هشدار اطلاع رسانی",
            ["shield"] = "امنیت محافظت سپر",
            ["shield-check"] = "امنیت تایید محافظت اعتماد",
            ["lock-keyhole"] = "امنیت قفل رمزنگاری",
            ["fingerprint"] = "امنیت اثر انگشت احراز هویت",
            ["cloud"] = "ابر فضای ابری کلاد cloud",
            ["cloud-cog"] = "سرویس ابری فضای ابری کلاد cloud",
            ["brain-circuit"] = "هوش مصنوعی ai یادگیری ماشین مغز",
            ["bot"] = "هوش مصنوعی ai ربات",
            ["sparkles"] = "هوش مصنوعی ai جادویی جدید",
            ["server"] = "سرور میزبانی هاست",
            ["database"] = "دیتابیس پایگاه داده بانک اطلاعاتی",
            ["code-xml"] = "کد برنامه نویسی توسعه",
            ["users"] = "کاربران مشتریان اعضا",
            ["user-round"] = "کاربر مشتری حساب",
            ["circle-check"] = "تایید موفق درست وضعیت",
            ["circle-x"] = "رد ناموفق خطا وضعیت",
            ["triangle-alert"] = "هشدار اخطار خطر",
            ["calendar-days"] = "تقویم تاریخ برنامه",
            ["clock"] = "زمان ساعت مدت",
            ["folder"] = "پوشه فولدر",
            ["file-text"] = "فایل سند متن",
            ["image"] = "تصویر عکس رسانه",
            ["chart-no-axes-column"] = "نمودار آمار گزارش تحلیل",
            ["scale"] = "قانون حقوقی عدالت ترازو",
            ["graduation-cap"] = "آموزش دانشگاه دانشجو",
            ["heart-pulse"] = "سلامت پزشکی درمان",
            ["plane"] = "سفر هواپیما پرواز",
            ["car"] = "خودرو ماشین حمل و نقل",
            ["sun"] = "خورشید هوا روشنایی روز",
            ["cloud-rain"] = "باران هوا ابری"
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, int> Popularity =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["search"] = 100, ["home"] = 99, ["settings"] = 98, ["user"] = 97,
            ["users"] = 96, ["shopping-cart"] = 95, ["package"] = 94,
            ["wallet"] = 93, ["credit-card"] = 92, ["gamepad-2"] = 91,
            ["ticket"] = 90, ["message-circle"] = 89, ["bell"] = 88,
            ["shield-check"] = 87, ["circle-check"] = 86, ["triangle-alert"] = 85,
            ["pencil"] = 84, ["trash-2"] = 83, ["plus"] = 82, ["x"] = 81,
            ["cloud"] = 80, ["brain-circuit"] = 79, ["headphones"] = 78
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    private static readonly FrozenDictionary<string, LucideIconEntry> ByKey = BuildEntries();
    public static IReadOnlyList<LucideIconEntry> Entries { get; } = ByKey.Values.OrderBy(x => x.Key, StringComparer.Ordinal).ToArray();
    public static int Count => Entries.Count;

    public static bool IsOfficialKey(string? value) =>
        TryCanonicalize(value, out var canonical) && ByKey.ContainsKey(canonical);

    public static bool TryNormalizeKey(string? value, out string normalized, bool includeLegacy = true)
    {
        normalized = string.Empty;
        if (!TryCanonicalize(value, out var canonical)) return false;
        if (ByKey.ContainsKey(canonical))
        {
            normalized = canonical;
            return true;
        }
        if (includeLegacy && LegacyMappings.TryGetValue(canonical, out var replacement) && ByKey.ContainsKey(replacement))
        {
            normalized = replacement;
            return true;
        }
        return false;
    }

    public static bool IsLegacyKey(string? value, out string? replacement)
    {
        replacement = null;
        if (!TryCanonicalize(value, out var canonical) || ByKey.ContainsKey(canonical)) return false;
        if (!LegacyMappings.TryGetValue(canonical, out var mapped) || !ByKey.ContainsKey(mapped)) return false;
        replacement = mapped;
        return true;
    }

    public static string ResolveOrFallback(string? value, string fallbackIconKey = "circle-question-mark")
    {
        if (TryNormalizeKey(value, out var normalized)) return normalized;
        return TryNormalizeKey(fallbackIconKey, out var fallback) ? fallback : "circle";
    }

    public static LucideIconEntry? Find(string? value) =>
        TryNormalizeKey(value, out var normalized) && ByKey.TryGetValue(normalized, out var entry) ? entry : null;

    public static IReadOnlyList<LucideIconEntry> Search(
        string? query,
        string? category = null,
        int maxResults = 500,
        IEnumerable<string>? excludedKeys = null)
    {
        maxResults = Math.Clamp(maxResults, 1, Count);
        var excluded = excludedKeys is null
            ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            : excludedKeys.Select(x => TryNormalizeKey(x, out var key) ? key : null)
                .OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedQuery = NormalizeSearch(query);
        var tokens = normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return Entries
            .Where(x => excluded.Count == 0 || !excluded.Contains(x.Key))
            .Where(x => string.IsNullOrWhiteSpace(category) || string.Equals(x.Category, category, StringComparison.OrdinalIgnoreCase))
            .Select(x => (Entry: x, Score: Score(x, normalizedQuery, tokens)))
            .Where(x => tokens.Length == 0 || x.Score >= 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Entry.Popularity)
            .ThenBy(x => x.Entry.Key, StringComparer.Ordinal)
            .Take(maxResults)
            .Select(x => x.Entry)
            .ToArray();
    }

    public static string NormalizeSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var form = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(form.Length);
        var space = false;
        foreach (var raw in form)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(raw) == UnicodeCategory.NonSpacingMark) continue;
            var ch = raw switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                '\u200c' or '-' or '_' or '/' or '\\' => ' ',
                _ => raw
            };
            if (char.IsWhiteSpace(ch))
            {
                if (!space && builder.Length > 0) builder.Append(' ');
                space = true;
            }
            else
            {
                builder.Append(ch);
                space = false;
            }
        }
        return builder.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    private static FrozenDictionary<string, LucideIconEntry> BuildEntries()
    {
        var entries = new Dictionary<string, LucideIconEntry>(LucideIconCatalogData.Items.Length, StringComparer.OrdinalIgnoreCase);
        foreach (var (key, rawTags) in LucideIconCatalogData.Items)
        {
            var tags = rawTags.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var aliases = PersianAliases.GetValueOrDefault(key, string.Empty);
            var category = DetectCategory(key, rawTags, aliases);
            var englishName = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(key.Replace('-', ' '));
            var popularity = Popularity.GetValueOrDefault(key, 0);
            var search = NormalizeSearch($"{key} {englishName} {rawTags} {aliases}");
            entries[key] = new LucideIconEntry(key, englishName, aliases, category, tags, popularity, search);
        }
        return entries.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static int Score(LucideIconEntry entry, string query, IReadOnlyList<string> tokens)
    {
        if (tokens.Count == 0) return entry.Popularity;
        var normalizedKey = NormalizeSearch(entry.Key);
        if (normalizedKey == query) return 10_000 + entry.Popularity;

        var total = 0;
        foreach (var token in tokens)
        {
            if (normalizedKey == token) total += 2_000;
            else if (normalizedKey.StartsWith(token, StringComparison.Ordinal)) total += 1_500;
            else if (entry.NormalizedSearchText.Contains(token, StringComparison.Ordinal)) total += 1_000;
            else if (Compact(entry.NormalizedSearchText).Contains(Compact(token), StringComparison.Ordinal)) total += 700;
            else if (IsSubsequence(token, entry.NormalizedSearchText)) total += 200;
            else return -1;
        }
        return total + entry.Popularity;
    }

    private static bool TryCanonicalize(string? value, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 100) return false;
        canonical = value.Trim().ToLowerInvariant().Replace('_', '-').Replace(' ', '-');
        while (canonical.Contains("--", StringComparison.Ordinal)) canonical = canonical.Replace("--", "-", StringComparison.Ordinal);
        return canonical.All(ch => ch is >= 'a' and <= 'z' or >= '0' and <= '9' or '-');
    }

    private static string Compact(string value) => value.Replace(" ", string.Empty, StringComparison.Ordinal);

    private static bool IsSubsequence(string needle, string haystack)
    {
        var index = 0;
        foreach (var ch in haystack)
            if (index < needle.Length && needle[index] == ch) index++;
        return index == needle.Length;
    }

    private static string DetectCategory(string key, string tags, string aliases)
    {
        var text = $" {key} {tags} {aliases} ".ToLowerInvariant();
        if (Has(text, "arrow", "chevron", "corner", "move-", "undo", "redo")) return "Arrows";
        if (Has(text, "game", "joystick", "dice", "trophy", "puzzle", "chess")) return "Gaming";
        if (Has(text, "wallet", "coins", "piggy-bank")) return "Wallet";
        if (Has(text, "credit-card", "banknote", "badge-dollar", "circle-dollar", "landmark")) return "Payments";
        if (Has(text, "shopping", "store", "basket", "package", "barcode", "receipt")) return "Shopping";
        if (Has(text, "cloud")) return "Cloud";
        if (Has(text, "brain", "bot", "sparkles", "wand", "scan-search")) return "AI";
        if (Has(text, "database", "table-properties")) return "Database";
        if (Has(text, "server", "container", "hard-drive")) return "Server";
        if (Has(text, "wifi", "router", "network", "antenna", "radio-tower", "ethernet")) return "Network";
        if (Has(text, "shield", "lock", "key-round", "fingerprint", "scan-face", "security")) return "Security";
        if (Has(text, "smartphone", "tablet", "monitor", "laptop", "watch", "keyboard", "mouse")) return "Devices";
        if (Has(text, "code", "terminal", "git-", "braces", "bug", "binary", "file-json")) return "Development";
        if (Has(text, "mail", "at-sign", "inbox")) return "Email";
        if (Has(text, "message-square-text", "sms")) return "SMS";
        if (Has(text, "message", "messages", "speech", "chat")) return "Chat";
        if (Has(text, "headphones", "headset", "life-buoy", "concierge")) return "Support";
        if (Has(text, "ticket")) return "Tickets";
        if (Has(text, "bell", "notification")) return "Notifications";
        if (Has(text, "user", "contact", "person", "baby")) return "Users";
        if (Has(text, "phone", "send", "radio", "rss", "megaphone")) return "Communication";
        if (Has(text, "calendar")) return "Calendar";
        if (Has(text, "clock", "alarm", "timer", "hourglass", "history")) return "Time";
        if (Has(text, "folder")) return "Folders";
        if (Has(text, "file", "clipboard", "notebook")) return "Files";
        if (Has(text, "image", "camera", "aperture", "scan-line", "wallpaper")) return "Images";
        if (Has(text, "video", "music", "audio", "volume", "play", "pause", "mic", "podcast")) return "Media";
        if (Has(text, "chart", "trending", "activity", "gauge")) return "Charts";
        if (Has(text, "analytics", "scan-search", "funnel")) return "Analytics";
        if (Has(text, "briefcase", "building", "factory", "presentation", "handshake")) return "Business";
        if (Has(text, "scale", "gavel", "landmark", "copyright", "badge", "scroll-text")) return "Legal";
        if (Has(text, "graduation", "school", "book", "library", "pencil", "ruler")) return "Education";
        if (Has(text, "hospital", "stethoscope", "heart-pulse", "pill", "syringe", "dna", "ambulance")) return "Health";
        if (Has(text, "plane", "luggage", "hotel", "map", "compass", "tent")) return "Travel";
        if (Has(text, "car", "bus", "train", "bike", "ship", "truck", "fuel", "traffic")) return "Transport";
        if (Has(text, "sun", "moon", "rain", "snow", "cloud", "wind", "umbrella", "thermometer")) return "Weather";
        if (Has(text, "share", "thumb", "smile", "heart", "link", "frown", "laugh", "angry")) return "Social/Semantic";
        if (Has(text, "check", "circle-x", "badge-check", "loader", "progress")) return "Status";
        if (Has(text, "alert", "triangle", "octagon", "siren", "circle-help", "info")) return "Alerts";
        if (Has(text, "plus", "minus", "trash", "pencil", "copy", "download", "upload", "save", "search", "filter")) return "Actions";
        if (Has(text, "layout", "panel", "menu", "ellipsis", "sidebar", "columns", "rows")) return "Interface";
        if (Has(text, "tag", "percent", "gift", "badge", "hand-coins")) return "Commerce";
        if (Has(text, "circle", "square", "triangle", "shapes", "component", "box", "dot")) return "General";
        return "Miscellaneous";
    }

    private static bool Has(string source, params string[] terms) => terms.Any(source.Contains);
}
