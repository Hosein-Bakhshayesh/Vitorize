using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class VitorizeSeedService : IVitorizeSeedService
    {
        private readonly VitorizeDbContext _dbContext;
        public VitorizeSeedService(VitorizeDbContext dbContext) => _dbContext = dbContext;

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await SeedRolesAsync(cancellationToken);
            await SeedSettingsAsync(cancellationToken);
            await SeedFontAssetsAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);

            // کاربران پیش‌فرض پس از ذخیره‌ی نقش‌ها ساخته می‌شوند تا نقش‌ها قابل واکشی باشند.
            await SeedDefaultUsersAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task SeedDefaultUsersAsync(CancellationToken cancellationToken)
        {
            await SeedUserAsync(
                mobile: "09123456789",
                fullName: "مدیر سیستم",
                password: "12345678",
                roleNames: new[] { "SuperAdmin", "Admin" },
                cancellationToken);

            await SeedUserAsync(
                mobile: "09378149896",
                fullName: "مشتری نمونه",
                password: "123456",
                roleNames: new[] { "Customer" },
                cancellationToken);
        }

        private async Task SeedUserAsync(
            string mobile,
            string fullName,
            string password,
            string[] roleNames,
            CancellationToken cancellationToken)
        {
            // اگر کاربر از قبل وجود دارد هیچ‌چیزی بازنویسی نمی‌شود (ایمن برای Production).
            var exists = await _dbContext.Users
                .AnyAsync(x => x.Mobile == mobile, cancellationToken);

            if (exists)
                return;

            var roles = await _dbContext.Roles
                .Where(x => roleNames.Contains(x.Name))
                .ToListAsync(cancellationToken);

            var user = new User
            {
                Id = Guid.NewGuid(),
                FullName = fullName,
                Mobile = mobile,
                PasswordHash = PasswordHasher.Hash(password),
                Status = (byte)Vitorize.Shared.Enums.UserStatus.Active,
                VerificationStatus = (byte)Vitorize.Shared.Enums.VerificationStatus.Pending,
                IsMobileConfirmed = true,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            foreach (var role in roles)
                user.Roles.Add(role);

            await _dbContext.Users.AddAsync(user, cancellationToken);
        }

        private async Task SeedRolesAsync(CancellationToken cancellationToken)
        {
            var roles = new[]
            {
                ("SuperAdmin", "مدیر کل"),
                ("Admin", "مدیر فروشگاه"),
                ("Support", "پشتیبان"),
                ("Customer", "مشتری")
            };

            foreach (var role in roles)
            {
                var exists = await _dbContext.Roles.AnyAsync(x => x.Name == role.Item1, cancellationToken);
                if (!exists)
                {
                    await _dbContext.Roles.AddAsync(new Role
                    {
                        Id = Guid.NewGuid(),
                        Name = role.Item1,
                        DisplayName = role.Item2,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
            }
        }

        private async Task SeedSettingsAsync(CancellationToken cancellationToken)
        {
            // شناسه واقعی در کد seed نمی‌شود. هر چهار کلید OTP و هر نه کلید اعلان
            // با یک مقدار پیش‌فرض مشترک ساخته می‌شوند و هنگام ذخیره ادمین همگام می‌مانند.
            const string universalOtpTemplateId = "";
            const string universalNotificationTemplateId = "";

            var settings = new[]
            {
                // ───────────── General ─────────────
                S("SiteName", "ویتورایز", "General", "string", "نام فروشگاه"),
                S("SiteDescription", "فروشگاه گیفت کارت و سرویس‌های دیجیتال", "General", "string", "توضیح کوتاه فروشگاه"),
                S("MaintenanceMode", "false", "General", "bool", "حالت تعمیر و نگهداری (نمایش صفحه ۵۰۳ به بازدیدکنندگان)"),
                S("MaintenanceMessage", "به‌زودی با نسخه‌ای بهتر برمی‌گردیم. از صبوری شما سپاسگزاریم.", "General", "string", "پیام صفحه حالت تعمیر"),

                // ───────────── Branding ─────────────
                S("SiteTagline", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین", "Branding", "string", "شعار سایت (کنار لوگو و عنوان صفحات)"),
                S("SiteLogoPath", "", "Branding", "string", "مسیر لوگوی سایت (خالی = لوگوی پیش‌فرض)"),
                S("BrandPrimaryColor", "", "Branding", "color", "رنگ اصلی برند (خالی = رنگ پیش‌فرض تم)"),
                S("FooterDescription", "بازارگاه دیجیتال گیمینگ و خدمات آنلاین؛ خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی.", "Branding", "string", "توضیح فوتر"),
                S("CopyrightText", "تمامی حقوق برای ویتورایز محفوظ است.", "Branding", "string", "متن کپی‌رایت فوتر"),

                // ───────────── Logos & Images ─────────────
                S("LogoPath", "", "Logos", "image", "لوگوی اصلی (تم روشن) — خالی = لوگوی پیش‌فرض"),
                S("LogoDarkPath", "", "Logos", "image", "لوگوی تم تیره"),
                S("LogoSmallPath", "", "Logos", "image", "لوگوی کوچک / آیکون (نوار بالا، موبایل)"),
                S("HeaderLogoPath", "", "Logos", "image", "لوگوی هدر (خالی = لوگوی اصلی)"),
                S("FooterLogoPath", "", "Logos", "image", "لوگوی فوتر (خالی = لوگوی اصلی)"),
                S("FaviconPath", "", "Logos", "image", "فاوآیکون سایت"),
                S("AppleTouchIconPath", "", "Logos", "image", "آیکون Apple Touch"),
                S("OgImagePath", "", "Logos", "image", "تصویر OpenGraph (اشتراک‌گذاری)"),
                S("TwitterImagePath", "", "Logos", "image", "تصویر توییتر / X"),
                S("SocialPreviewImagePath", "", "Logos", "image", "تصویر پیش‌نمایش شبکه‌های اجتماعی"),
                S("HeroBackgroundPath", "", "Logos", "image", "تصویر پس‌زمینه Hero صفحه اول"),
                S("Error404IllustrationPath", "", "Logos", "image", "تصویر صفحه ۴۰۴ (خالی = ماسکات پیش‌فرض)"),
                S("Error500IllustrationPath", "", "Logos", "image", "تصویر صفحه ۵۰۰ (خالی = ماسکات پیش‌فرض)"),
                S("MaintenanceIllustrationPath", "", "Logos", "image", "تصویر صفحه تعمیر و نگهداری"),
                S("EmptyStateIllustrationPath", "", "Logos", "image", "تصویر پیش‌فرض حالت‌های خالی"),
                S("Branding.AssetVersion", "1", "Branding", "string", "نسخه کش لوگوها و آیکون‌های برند"),

                // ───────────── Typography ─────────────
                S("Typography.FontFamily", "Vazirmatn", "Typography", "string", "نام فونت فعال؛ پیش‌فرض Vazirmatn"),
                S("Typography.FontPath", "", "Typography", "string", "مسیر فایل فونت فعال؛ خالی یعنی فونت داخلی"),
                S("Typography.FontFormat", "woff2", "Typography", "string", "فرمت فایل فونت فعال"),
                S("Typography.Scope", "3", "Typography", "int", "محدوده اعمال فونت: ۱ فروشگاه، ۲ مدیریت، ۳ کل برنامه"),
                S("Typography.Version", "1", "Typography", "string", "نسخه کش فونت"),
                S("Typography.MaxUploadMb", "5", "Typography", "int", "حداکثر حجم آپلود فونت بر حسب مگابایت"),

                // ───────────── Iranian trust seals (safe link + image only) ─────────────
                S("TrustSeal.Enamad.Enabled", "false", "TrustSeals", "bool", "نمایش نماد اعتماد الکترونیکی"),
                S("TrustSeal.Enamad.Title", "نماد اعتماد الکترونیکی", "TrustSeals", "string", "عنوان نماد"),
                S("TrustSeal.Enamad.Url", "", "TrustSeals", "string", "نشانی تأیید Enamad؛ فقط دامنه enamad.ir"),
                S("TrustSeal.Enamad.ImagePath", "", "TrustSeals", "image", "تصویر نماد Enamad"),
                S("TrustSeal.Enamad.Alt", "نماد اعتماد الکترونیکی", "TrustSeals", "string", "متن جایگزین تصویر"),
                S("TrustSeal.Enamad.SortOrder", "10", "TrustSeals", "int", "ترتیب نمایش"),
                S("TrustSeal.Enamad.NewTab", "true", "TrustSeals", "bool", "باز شدن در زبانه جدید"),
                S("TrustSeal.Ecunion.Enabled", "false", "TrustSeals", "bool", "نمایش مجوز اتحادیه کشوری کسب‌وکارهای مجازی"),
                S("TrustSeal.Ecunion.Title", "اتحادیه کسب‌وکارهای مجازی", "TrustSeals", "string", "عنوان مجوز"),
                S("TrustSeal.Ecunion.Url", "", "TrustSeals", "string", "نشانی تأیید؛ فقط دامنه ecunion.ir"),
                S("TrustSeal.Ecunion.ImagePath", "", "TrustSeals", "image", "تصویر مجوز ecunion"),
                S("TrustSeal.Ecunion.Alt", "مجوز اتحادیه کسب‌وکارهای مجازی", "TrustSeals", "string", "متن جایگزین تصویر"),
                S("TrustSeal.Ecunion.SortOrder", "20", "TrustSeals", "int", "ترتیب نمایش"),
                S("TrustSeal.Ecunion.NewTab", "true", "TrustSeals", "bool", "باز شدن در زبانه جدید"),
                S("TrustSeal.Samandehi.Enabled", "false", "TrustSeals", "bool", "نمایش نشان ساماندهی"),
                S("TrustSeal.Samandehi.Title", "نشان ملی ثبت رسانه‌های دیجیتال", "TrustSeals", "string", "عنوان نشان"),
                S("TrustSeal.Samandehi.Url", "", "TrustSeals", "string", "نشانی تأیید؛ فقط دامنه samandehi.ir"),
                S("TrustSeal.Samandehi.ImagePath", "", "TrustSeals", "image", "تصویر نشان ساماندهی"),
                S("TrustSeal.Samandehi.Alt", "نشان ساماندهی", "TrustSeals", "string", "متن جایگزین تصویر"),
                S("TrustSeal.Samandehi.SortOrder", "30", "TrustSeals", "int", "ترتیب نمایش"),
                S("TrustSeal.Samandehi.NewTab", "true", "TrustSeals", "bool", "باز شدن در زبانه جدید"),

                // ───────────── SEO ─────────────
                S("MetaTitle", "ویتورایز | بازارگاه دیجیتال گیمینگ و خدمات آنلاین", "SEO", "string", "عنوان متای پیش‌فرض"),
                S("MetaDescription", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.", "SEO", "string", "توضیح متای پیش‌فرض"),
                S("MetaKeywords", "گیفت کارت, اشتراک, خدمات دیجیتال, بازی, گیمینگ, ویتورایز", "SEO", "string", "کلمات کلیدی پیش‌فرض"),
                S("SeoTitleTemplate", "{page} | {site}", "SEO", "string", "قالب عنوان صفحات ({page} و {site})"),
                S("GoogleAnalyticsId", "", "SEO", "string", "شناسه Google Analytics"),

                // ───────────── Homepage ─────────────
                S("HeroKicker", "ویتورایز · بازارگاه دیجیتال", "Homepage", "string", "متن کوچک بالای عنوان Hero"),
                S("HeroTitle", "دنیای بازی و دیجیتال در دستان تو", "Homepage", "string", "عنوان اصلی Hero صفحه اول"),
                S("HeroSubtitle", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.", "Homepage", "string", "زیرعنوان Hero صفحه اول"),
                S("HeroCtaText", "ورود به فروشگاه", "Homepage", "string", "متن دکمه اصلی Hero"),
                S("HeroCtaUrl", "/shop", "Homepage", "string", "لینک دکمه اصلی Hero"),
                S("HeroSecondaryCtaText", "دسته‌بندی‌ها", "Homepage", "string", "متن دکمه دوم Hero"),
                S("HeroSecondaryCtaUrl", "/categories", "Homepage", "string", "لینک دکمه دوم Hero"),
                S("NewsletterTitle", "از جدیدترین‌ها باخبر شو", "Homepage", "string", "عنوان بخش خبرنامه"),
                S("NewsletterSubtitle", "با عضویت در خبرنامه، از تخفیف‌ها و محصولات تازه زودتر از همه مطلع شو.", "Homepage", "string", "زیرعنوان بخش خبرنامه"),
                S("NewsletterCtaText", "عضویت", "Homepage", "string", "متن دکمه خبرنامه"),
                S("NewsletterPlaceholder", "ایمیل خود را وارد کنید", "Homepage", "string", "متن راهنمای ورودی خبرنامه"),

                // ───────────── About ─────────────
                S("AboutTitle", "درباره ویتورایز", "About", "string", "عنوان بخش درباره ما"),
                S("AboutText", "ویتورایز بازارگاهی دیجیتال برای خرید امن و آنی گیفت کارت، اشتراک و خدمات آنلاین است.", "About", "string", "متن درباره ما"),

                // ───────────── Trust badges & features (JSON) ─────────────
                S("TrustBadgesJson",
                  "[{\"icon\":\"shield-check\",\"title\":\"تضمین اصالت\",\"text\":\"محصولات رسمی و اورجینال\"},{\"icon\":\"zap\",\"title\":\"تحویل آنی\",\"text\":\"سریع و بدون انتظار\"},{\"icon\":\"headphones\",\"title\":\"پشتیبانی ۲۴/۷\",\"text\":\"همیشه کنار شما\"},{\"icon\":\"lock\",\"title\":\"پرداخت امن\",\"text\":\"درگاه‌های معتبر\"}]",
                  "Trust", "json", "نشان‌های اعتماد (آرایه JSON: icon,title,text)"),
                S("HomeFeaturesKicker", "چرا ویتورایز؟", "Trust", "string", "برچسب کوچک بخش «چرا ما»"),
                S("HomeFeaturesTitle", "خرید دیجیتال، ساده و مطمئن", "Trust", "string", "عنوان بخش «چرا ما»"),
                S("HomeFeaturesJson",
                  "[{\"icon\":\"grid\",\"title\":\"انتخاب محصول\",\"text\":\"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن.\"},{\"icon\":\"credit-card\",\"title\":\"پرداخت امن\",\"text\":\"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده.\"},{\"icon\":\"zap\",\"title\":\"تحویل آنی\",\"text\":\"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود.\"}]",
                  "Trust", "json", "مراحل / ویژگی‌های صفحه اول (آرایه JSON: icon,title,text)"),

                // ───────────── Footer ─────────────
                S("FooterText", "", "Footer", "string", "متن آزاد اضافی فوتر"),

                // ───────────── Social media ─────────────
                S("InstagramUrl", "https://instagram.com/vitorize", "Social", "string", "صفحه اینستاگرام"),
                S("TelegramUrl", "https://t.me/vitorize", "Social", "string", "کانال تلگرام"),
                S("WhatsAppUrl", "", "Social", "string", "واتساپ"),
                S("XUrl", "", "Social", "string", "X (توییتر)"),
                S("LinkedInUrl", "", "Social", "string", "لینکدین"),
                S("DiscordUrl", "", "Social", "string", "دیسکورد"),
                S("YouTubeUrl", "", "Social", "string", "یوتیوب"),
                S("FacebookUrl", "", "Social", "string", "فیسبوک"),

                // ───────────── Contact ─────────────
                S("SupportEmail", "support@vitorize.com", "Contact", "string", "ایمیل پشتیبانی"),
                S("SupportPhone", "02100000000", "Contact", "string", "شماره پشتیبانی"),
                S("ContactAddress", "", "Contact", "string", "آدرس"),
                S("WorkingHours", "شنبه تا پنجشنبه، ۹ تا ۱۸", "Contact", "string", "ساعات کاری"),

                // ───────────── Empty-state texts ─────────────
                S("EmptyCartText", "سبد خرید شما خالی است.", "Empty", "string", "متن سبد خرید خالی"),
                S("EmptyWishlistText", "هنوز محصولی به علاقه‌مندی‌ها اضافه نکرده‌اید.", "Empty", "string", "متن علاقه‌مندی خالی"),
                S("EmptyOrdersText", "هنوز سفارشی ثبت نکرده‌اید.", "Empty", "string", "متن سفارش‌های خالی"),
                S("EmptySearchText", "نتیجه‌ای برای جستجوی شما پیدا نشد.", "Empty", "string", "متن جستجوی بدون نتیجه"),
                S("EmptyNotificationsText", "اعلان جدیدی ندارید.", "Empty", "string", "متن اعلان خالی"),
                S("EmptyTicketsText", "تیکتی ثبت نکرده‌اید.", "Empty", "string", "متن تیکت خالی"),
                S("EmptyReviewsText", "هنوز نظری ثبت نشده است.", "Empty", "string", "متن نظرات خالی"),
                S("NoProductsText", "محصولی برای نمایش وجود ندارد.", "Empty", "string", "متن نبود محصول"),

                // ───────────── Error / status page texts ─────────────
                S("Error404Title", "صفحه پیدا نشد", "Errors", "string", "عنوان صفحه ۴۰۴"),
                S("Error404Text", "صفحه‌ای که دنبال آن هستید وجود ندارد یا منتقل شده است.", "Errors", "string", "متن صفحه ۴۰۴"),
                S("Error400Title", "درخواست نامعتبر", "Errors", "string", "عنوان صفحه ۴۰۰"),
                S("Error400Text", "درخواست شما معتبر نیست. لطفاً دوباره تلاش کنید.", "Errors", "string", "متن صفحه ۴۰۰"),
                S("Error401Title", "نیاز به ورود", "Errors", "string", "عنوان صفحه ۴۰۱"),
                S("Error401Text", "برای مشاهده این صفحه ابتدا وارد حساب کاربری شوید.", "Errors", "string", "متن صفحه ۴۰۱"),
                S("Error403Title", "دسترسی مجاز نیست", "Errors", "string", "عنوان صفحه ۴۰۳"),
                S("Error403Text", "شما اجازه دسترسی به این بخش را ندارید.", "Errors", "string", "متن صفحه ۴۰۳"),
                S("Error500Title", "خطای غیرمنتظره", "Errors", "string", "عنوان صفحه ۵۰۰"),
                S("Error500Text", "مشکلی در سرور رخ داد. تیم ما در حال بررسی است.", "Errors", "string", "متن صفحه ۵۰۰"),
                S("Error503Title", "در حال به‌روزرسانی", "Errors", "string", "عنوان صفحه ۵۰۳ (تعمیر)"),
                S("Error503Text", "سایت موقتاً در دسترس نیست. به‌زودی برمی‌گردیم.", "Errors", "string", "متن صفحه ۵۰۳"),
                S("SessionExpiredTitle", "نشست شما منقضی شد", "Errors", "string", "عنوان نشست منقضی"),
                S("SessionExpiredText", "برای ادامه دوباره وارد شوید.", "Errors", "string", "متن نشست منقضی"),
                S("NetworkErrorTitle", "خطای ارتباط", "Errors", "string", "عنوان خطای شبکه"),
                S("NetworkErrorText", "ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید.", "Errors", "string", "متن خطای شبکه"),
                S("OfflineTitle", "اتصال اینترنت قطع است", "Errors", "string", "عنوان حالت آفلاین"),
                S("OfflineText", "به نظر می‌رسد اینترنت شما قطع شده است.", "Errors", "string", "متن حالت آفلاین"),
                S("PageRemovedTitle", "این صفحه حذف شده است", "Errors", "string", "عنوان صفحه حذف‌شده"),
                S("PageRemovedText", "محتوایی که دنبال آن بودید دیگر در دسترس نیست.", "Errors", "string", "متن صفحه حذف‌شده"),

                // ───────────── Custom scripts (public head/footer) ─────────────
                S("CustomHeadHtml", "", "Scripts", "string", "کد سفارشی داخل <head> (تحلیل، تگ‌ها)"),
                S("CustomFooterHtml", "", "Scripts", "string", "کد سفارشی انتهای صفحه"),

                // ───────────── Features (public flags) ─────────────
                S("EnableRegistration", "true", "Features", "bool", "ثبت‌نام کاربران"),
                S("EnableWallet", "true", "Features", "bool", "کیف پول کاربران"),

                // ───────────── Newsletter / SMS (legacy flags) ─────────────
                S("SmsEnabled", "false", "SMS", "bool", "ارسال پیامک (کلید قدیمی؛ از Sms.IsEnabled استفاده کنید)"),
                S("SmsProvider", "Mock", "SMS", "string", "ارائه‌دهنده پیامک (کلید قدیمی)"),

                // ───────────── SMS.ir (تنظیمات اصلی پیامک) ─────────────
                // نکته امنیتی: گروه «SMS» در endpoint عمومی تنظیمات قرار ندارد و هرگز آشکار نمی‌شود.
                S(SmsSettingKeys.IsEnabled, "false", "SMS", "bool", "فعال‌سازی سرویس پیامک SMS.ir"),
                S(SmsSettingKeys.Provider, "SMS.ir", "SMS", "string", "ارائه‌دهنده پیامک"),
                S(SmsSettingKeys.ApiKey, "", "SMS", "secret", "کلید API پنل SMS.ir (محرمانه)"),
                S(SmsSettingKeys.DefaultLineNumber, "", "SMS", "string", "شماره خط اختصاصی برای پیامک متنی (محرمانه)"),
                S(SmsSettingKeys.SenderName, "ویتورایز", "SMS", "string", "نام فرستنده (برای متن پیام)"),

                // Template IDs (شناسه قالب‌های تاییدشده در پنل SMS.ir)
                S(SmsSettingKeys.OtpTemplateId, universalOtpTemplateId, "SMS", "int", "شناسه قالب کد یکبار مصرف"),
                S(SmsSettingKeys.NotificationTemplateId, universalNotificationTemplateId, "SMS", "int", "شناسه قالب اطلاع‌رسانی عمومی"),
                S(SmsSettingKeys.LoginOtpTemplateId, universalOtpTemplateId, "SMS", "int", "کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)"),
                S(SmsSettingKeys.RegisterOtpTemplateId, universalOtpTemplateId, "SMS", "int", "کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)"),
                S(SmsSettingKeys.ForgotPasswordTemplateId, universalOtpTemplateId, "SMS", "int", "کلید سازگاری قالب OTP؛ همگام با Sms.OtpTemplateId (CODE، EXPIRE)"),
                S(SmsSettingKeys.OrderPaidTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.OrderCompletedTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.OrderStatusChangedTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.GiftCodeDeliveredTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.TicketReplyTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.VerificationApprovedTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.VerificationRejectedTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),
                S(SmsSettingKeys.WalletTopUpSuccessTemplateId, universalNotificationTemplateId, "SMS", "int", "کلید سازگاری اطلاع رسانی؛ همگام با Sms.NotificationTemplateId (ORDER_NUMBER)"),

                // سیاست کد یکبار‌مصرف و پایداری
                S(SmsSettingKeys.OtpExpiryMinutes, "3", "SMS", "int", "مدت اعتبار کد یکبار‌مصرف (دقیقه)"),
                S(SmsSettingKeys.OtpResendCooldownSeconds, "90", "SMS", "int", "فاصله ارسال مجدد کد (ثانیه)"),
                S(SmsSettingKeys.OtpMaxAttempts, "5", "SMS", "int", "حداکثر تلاش مجاز برای هر کد"),
                S(SmsSettingKeys.DailyOtpLimitPerMobile, "10", "SMS", "int", "سقف کد روزانه برای هر شماره"),
                S(SmsSettingKeys.DailySmsLimitPerMobile, "30", "SMS", "int", "سقف پیامک روزانه برای هر شماره"),
                S(SmsSettingKeys.MaxRetryCount, "5", "SMS", "int", "حداکثر تعداد بازتلاش ارسال"),
                S(SmsSettingKeys.RetryDelaySeconds, "30", "SMS", "int", "پایه تأخیر بازتلاش (ثانیه)"),
                S(SmsSettingKeys.UseOutbox, "true", "SMS", "bool", "ارسال پیامک رویدادهای تجاری از طریق Outbox"),
                S(SmsSettingKeys.CustomSendEnabled, "false", "SMS", "bool", "فعال‌سازی ارسال پیامک سفارشی توسط مدیر"),
                S(SmsSettingKeys.CustomTextEnabled, "false", "SMS", "bool", "فعال‌سازی پیامک متنی سفارشی"),
                S(SmsSettingKeys.MaxCustomRecipients, "1", "SMS", "int", "حداکثر گیرنده در هر ارسال سفارشی"),
                S(SmsSettingKeys.MaxCustomTextLength, "500", "SMS", "int", "حداکثر طول پیامک متنی سفارشی"),
                S(SmsSettingKeys.RequireConfirmation, "true", "SMS", "bool", "نیاز به تایید نهایی پیش از ارسال سفارشی"),
                S(SmsSettingKeys.AllowImmediateSend, "false", "SMS", "bool", "اجازه ارسال فوری به جای صف"),
                S(SmsSettingKeys.HistoryRetentionDays, "180", "SMS", "int", "مدت نگهداری تاریخچه پیامک بر حسب روز"),
                S(SmsSettingKeys.MaskMobileInAdmin, "true", "SMS", "bool", "پنهان‌سازی شماره موبایل در تاریخچه مدیر"),
                S(SmsSettingKeys.AllowAdminViewFullMobile, "false", "SMS", "bool", "اجازه مشاهده شماره کامل برای مدیر کل"),
                S(SmsSettingKeys.AllowRetryFailed, "true", "SMS", "bool", "اجازه بازتلاش امن پیامک ناموفق"),
                S(SmsSettingKeys.LogSensitiveData, "false", "SMS", "bool", "لاگ‌کردن داده حساس (فقط برای توسعه؛ در Production خاموش)"),

                // ───────────── Email (SMTP) ─────────────
                S("SmtpHost", "", "Email", "string", "میزبان SMTP"),
                S("SmtpPort", "587", "Email", "int", "پورت SMTP"),
                S("SmtpUsername", "", "Email", "string", "نام کاربری SMTP"),
                S("SmtpFromEmail", "", "Email", "string", "ایمیل فرستنده"),
                S("SmtpFromName", "ویتورایز", "Email", "string", "نام فرستنده"),
                S("SmtpEnableSsl", "true", "Email", "bool", "استفاده از SSL"),

                // ───────────── Security ─────────────
                S("RequireEmailConfirmation", "false", "Security", "bool", "الزام تأیید ایمیل"),
                S("MinPasswordLength", "8", "Security", "int", "حداقل طول رمز عبور"),
                S("MaxLoginAttempts", "5", "Security", "int", "حداکثر تلاش ناموفق ورود"),

                // ───────────── Uploads ─────────────
                S("MaxUploadSizeMb", "2", "Uploads", "int", "حداکثر حجم آپلود (مگابایت)"),
                S("AllowedImageFormats", "jpg,jpeg,png,webp", "Uploads", "string", "فرمت‌های مجاز تصویر"),

                // ───────────── Wallet ─────────────
                S("WalletMinCharge", "100000", "Wallet", "decimal", "حداقل شارژ کیف پول"),
                S("WalletMaxCharge", "100000000", "Wallet", "decimal", "حداکثر شارژ کیف پول"),

                // ───────────── Payment ─────────────
                S("ZarinpalMerchantId", "", "Payment", "string", "شناسه پذیرنده زرین‌پال"),
                S("ZarinpalSandbox", "true", "Payment", "bool", "حالت آزمایشی زرین‌پال"),
                S("ZarinpalStartPayUrl", "https://sandbox.zarinpal.com/pg/StartPay", "Payment", "string", "آدرس شروع پرداخت زرین‌پال"),
                S("ZarinpalBaseUrl", "https://sandbox.zarinpal.com/pg/v4/payment", "Payment", "string", "آدرس اصلی زرین‌پال"),
                S("ZarinpalCallbackUrl", "https://localhost:7221/api/payments/zarinpal/callback", "Payment", "string", "آدرس بازگشت پرداخت زرین‌پال")
            };

            foreach (var item in settings)
            {
                var current = await _dbContext.Settings.FirstOrDefaultAsync(x => x.Key == item.Key, cancellationToken);
                if (current == null)
                {
                    await _dbContext.Settings.AddAsync(new Setting
                    {
                        Id = Guid.NewGuid(),
                        Key = item.Key,
                        Value = item.Value,
                        GroupName = item.GroupName,
                        ValueType = item.ValueType,
                        Description = item.Description,
                        UpdatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }
                else
                {
                    current.GroupName = string.IsNullOrWhiteSpace(current.GroupName) ? item.GroupName : current.GroupName;
                    current.ValueType = string.IsNullOrWhiteSpace(current.ValueType) ? item.ValueType : current.ValueType;
                    current.Description = SmsSettingKeys.TryGetTemplateIdGroup(item.Key, out _)
                        ? item.Description
                        : string.IsNullOrWhiteSpace(current.Description) ? item.Description : current.Description;
                }
            }
        }

        private static SeedSetting S(string key, string value, string groupName, string valueType, string description) =>
            new(key, value, groupName, valueType, description);

        private async Task SeedFontAssetsAsync(CancellationToken cancellationToken)
        {
            if (await _dbContext.FontAssets.AnyAsync(x => x.IsBuiltIn, cancellationToken)) return;
            await _dbContext.FontAssets.AddAsync(new FontAsset
            {
                Id = Guid.NewGuid(), FamilyName = "Vazirmatn", FilePath = null, FileFormat = "woff2",
                MimeType = "font/woff2", SizeBytes = 0, IsBuiltIn = true, IsActive = true,
                Scope = (byte)Vitorize.Shared.Enums.FontApplicationScope.EntireApplication,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        private sealed record SeedSetting(string Key, string Value, string GroupName, string ValueType, string Description);
    }
}
