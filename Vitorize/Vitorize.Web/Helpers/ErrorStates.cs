using Vitorize.Web.Services.UI;

namespace Vitorize.Web.Helpers
{
    public sealed record ErrorStateInfo(
        string Title,
        string Message,
        string? CodeDisplay,
        string IllustrationPath,
        string[] Actions);

    /// <summary>
    /// نگاشت کد/نوع خطا به متن‌های قابل‌تنظیم (از Settings)، تصویر ماسکات و دکمه‌های کمکی.
    /// همه‌ی متن‌ها از برندینگ عمومی خوانده می‌شوند تا کاملاً قابل‌ویرایش باشند.
    /// </summary>
    public static class ErrorStates
    {
        public static ErrorStateInfo Resolve(string? code, StoreBranding b)
        {
            var key = (code ?? "404").Trim().ToLowerInvariant();

            return key switch
            {
                "400" or "bad-request" => new(
                    b.Get("Error400Title", "درخواست نامعتبر"),
                    b.Get("Error400Text", "درخواست شما معتبر نیست. لطفاً دوباره تلاش کنید."),
                    "۴۰۰", b.EmptyStateIllustrationPath, new[] { "back", "home" }),

                "401" or "unauthorized" => new(
                    b.Get("Error401Title", "نیاز به ورود"),
                    b.Get("Error401Text", "برای مشاهده این صفحه ابتدا وارد حساب کاربری شوید."),
                    "۴۰۱", b.EmptyStateIllustrationPath, new[] { "login", "home" }),

                "403" or "access-denied" or "forbidden" => new(
                    b.Get("Error403Title", "دسترسی مجاز نیست"),
                    b.Get("Error403Text", "شما اجازه دسترسی به این بخش را ندارید."),
                    "۴۰۳", b.EmptyStateIllustrationPath, new[] { "home", "contact" }),

                "500" or "error" => new(
                    b.Get("Error500Title", "خطای غیرمنتظره"),
                    b.Get("Error500Text", "مشکلی در سرور رخ داد. تیم ما در حال بررسی است."),
                    "۵۰۰", b.Error500IllustrationPath, new[] { "retry", "home" }),

                "503" or "maintenance" => new(
                    b.Get("Error503Title", "در حال به‌روزرسانی"),
                    b.Get("Error503Text", "سایت موقتاً در دسترس نیست. به‌زودی برمی‌گردیم."),
                    "۵۰۳", b.MaintenanceIllustrationPath, new[] { "retry" }),

                "session" or "session-expired" => new(
                    b.Get("SessionExpiredTitle", "نشست شما منقضی شد"),
                    b.Get("SessionExpiredText", "برای ادامه دوباره وارد شوید."),
                    null, b.EmptyStateIllustrationPath, new[] { "login", "home" }),

                "network" => new(
                    b.Get("NetworkErrorTitle", "خطای ارتباط"),
                    b.Get("NetworkErrorText", "ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید."),
                    null, b.EmptyStateIllustrationPath, new[] { "retry", "home" }),

                "offline" => new(
                    b.Get("OfflineTitle", "اتصال اینترنت قطع است"),
                    b.Get("OfflineText", "به نظر می‌رسد اینترنت شما قطع شده است."),
                    null, b.EmptyStateIllustrationPath, new[] { "retry", "home" }),

                "removed" or "page-removed" or "gone" => new(
                    b.Get("PageRemovedTitle", "این صفحه حذف شده است"),
                    b.Get("PageRemovedText", "محتوایی که دنبال آن بودید دیگر در دسترس نیست."),
                    null, b.EmptyStateIllustrationPath, new[] { "home", "shop" }),

                // ── حالت‌های خالی (Empty states) ──
                "empty-cart" => new(
                    b.Get("EmptyCartText", "سبد خرید شما خالی است."),
                    "محصولات مورد علاقه‌ات را به سبد اضافه کن و خرید را کامل کن.",
                    null, b.EmptyStateIllustrationPath, new[] { "shop", "home" }),

                "empty-wishlist" => new(
                    b.Get("EmptyWishlistText", "هنوز محصولی به علاقه‌مندی‌ها اضافه نکرده‌اید."),
                    "محصولاتی که دوست داری را نشان کن تا بعداً راحت پیدایشان کنی.",
                    null, b.EmptyStateIllustrationPath, new[] { "shop" }),

                "empty-orders" => new(
                    b.Get("EmptyOrdersText", "هنوز سفارشی ثبت نکرده‌اید."),
                    "اولین خریدت را از فروشگاه شروع کن.",
                    null, b.EmptyStateIllustrationPath, new[] { "shop" }),

                "empty-search" => new(
                    b.Get("EmptySearchText", "نتیجه‌ای برای جستجوی شما پیدا نشد."),
                    "عبارت دیگری را امتحان کن یا از دسته‌بندی‌ها کمک بگیر.",
                    null, b.EmptyStateIllustrationPath, new[] { "categories", "shop" }),

                "empty-notifications" => new(
                    b.Get("EmptyNotificationsText", "اعلان جدیدی ندارید."),
                    "", null, b.EmptyStateIllustrationPath, Array.Empty<string>()),

                "empty-tickets" => new(
                    b.Get("EmptyTicketsText", "تیکتی ثبت نکرده‌اید."),
                    "", null, b.EmptyStateIllustrationPath, Array.Empty<string>()),

                "empty-reviews" => new(
                    b.Get("EmptyReviewsText", "هنوز نظری ثبت نشده است."),
                    "", null, b.EmptyStateIllustrationPath, Array.Empty<string>()),

                "no-products" => new(
                    b.Get("NoProductsText", "محصولی برای نمایش وجود ندارد."),
                    "فیلترها را تغییر بده یا دسته‌بندی دیگری را ببین.",
                    null, b.EmptyStateIllustrationPath, new[] { "categories" }),

                _ => new( // 404 و هر مقدار ناشناخته
                    b.Get("Error404Title", "صفحه پیدا نشد"),
                    b.Get("Error404Text", "صفحه‌ای که دنبال آن هستید وجود ندارد یا منتقل شده است."),
                    "۴۰۴", b.Error404IllustrationPath, new[] { "home", "shop", "search" }),
            };
        }
    }
}
