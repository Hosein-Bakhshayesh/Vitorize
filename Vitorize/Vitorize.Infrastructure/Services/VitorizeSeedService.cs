using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class VitorizeSeedService : IVitorizeSeedService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly BootstrapAdminOptions _bootstrapAdmin;
        private readonly DevelopmentDemoUserOptions _developmentDemoUser;
        private readonly IHostEnvironment _environment;
        private readonly ILogger<VitorizeSeedService> _logger;

        public VitorizeSeedService(
            VitorizeDbContext dbContext,
            IOptions<BootstrapAdminOptions> bootstrapAdmin,
            IOptions<DevelopmentDemoUserOptions> developmentDemoUser,
            IHostEnvironment environment,
            ILogger<VitorizeSeedService> logger)
        {
            _dbContext = dbContext;
            _bootstrapAdmin = bootstrapAdmin.Value;
            _developmentDemoUser = developmentDemoUser.Value;
            _environment = environment;
            _logger = logger;
        }

        public async Task SeedAsync(CancellationToken cancellationToken = default)
        {
            await SeedReferenceDataAsync(cancellationToken);
            await BootstrapSuperAdminAsync(cancellationToken);
            await SeedDevelopmentDemoUserAsync(cancellationToken);
        }

        public async Task SeedReferenceDataAsync(CancellationToken cancellationToken = default)
        {
            await SeedRolesAsync(cancellationToken);
            await SeedSettingsAsync(cancellationToken);
            await SeedFontAssetsAsync(cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        private async Task BootstrapSuperAdminAsync(CancellationToken cancellationToken)
        {
            if (!_bootstrapAdmin.Enabled)
                return;

            IDbContextTransaction? transaction = null;
            if (_dbContext.Database.IsRelational())
            {
                transaction = await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable,
                    cancellationToken);
            }

            await using (transaction)
            {
                var superAdminExists = await _dbContext.Users
                    .AnyAsync(
                        user => user.Roles.Any(role => role.Name == "SuperAdmin"),
                        cancellationToken);

                if (superAdminExists)
                {
                    if (transaction != null)
                        await transaction.CommitAsync(cancellationToken);

                    return;
                }

                var credentials = ValidateCredentials(
                    BootstrapAdminOptions.SectionName,
                    _bootstrapAdmin.Mobile,
                    _bootstrapAdmin.Password,
                    _bootstrapAdmin.FullName);

                var mobileAlreadyExists = await _dbContext.Users
                    .AnyAsync(user => user.Mobile == credentials.Mobile, cancellationToken);

                if (mobileAlreadyExists)
                {
                    throw new InvalidOperationException(
                        "BootstrapAdmin cannot create the initial SuperAdmin because the configured mobile already belongs to an existing user. No existing user was changed.");
                }

                var superAdminRole = await _dbContext.Roles
                    .SingleAsync(role => role.Name == "SuperAdmin", cancellationToken);

                var user = CreateUser(credentials);
                user.Roles.Add(superAdminRole);

                await _dbContext.Users.AddAsync(user, cancellationToken);
                await _dbContext.SecurityLogs.AddAsync(new SecurityLog
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    EventType = "BootstrapSuperAdminCreated",
                    Description = "The initial SuperAdmin was created by the explicit one-time bootstrap process. Disable BootstrapAdmin:Enabled and remove the bootstrap secrets.",
                    IsSuccessful = true,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);

                await _dbContext.SaveChangesAsync(cancellationToken);

                if (transaction != null)
                    await transaction.CommitAsync(cancellationToken);
            }

            _logger.LogWarning(
                "The initial SuperAdmin was created by explicit bootstrap configuration. Disable BootstrapAdmin:Enabled and remove all BootstrapAdmin secret values now. EventType={EventType}",
                OperationalEventNames.BootstrapSuperAdminCreated);
        }

        private async Task SeedDevelopmentDemoUserAsync(CancellationToken cancellationToken)
        {
            if (!_developmentDemoUser.Enabled)
                return;

            if (!_environment.IsDevelopment())
            {
                _logger.LogWarning(
                    "DevelopmentDemoUser:Enabled was ignored because the application is not running in Development.");
                return;
            }

            var credentials = ValidateCredentials(
                DevelopmentDemoUserOptions.SectionName,
                _developmentDemoUser.Mobile,
                _developmentDemoUser.Password,
                _developmentDemoUser.FullName);

            var existingUser = await _dbContext.Users
                .AnyAsync(user => user.Mobile == credentials.Mobile, cancellationToken);

            if (existingUser)
                return;

            var customerRole = await _dbContext.Roles
                .SingleAsync(role => role.Name == "Customer", cancellationToken);

            var user = CreateUser(credentials);
            user.Roles.Add(customerRole);

            await _dbContext.Users.AddAsync(user, cancellationToken);
            await _dbContext.SecurityLogs.AddAsync(new SecurityLog
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                EventType = "DevelopmentDemoUserCreated",
                Description = "A demo customer was created by explicit Development-only configuration.",
                IsSuccessful = true,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "A demo customer was created from explicit Development-only configuration.");
        }

        private static BootstrapCredentials ValidateCredentials(
            string sectionName,
            string? mobile,
            string? password,
            string? fullName)
        {
            if (string.IsNullOrWhiteSpace(mobile) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fullName))
            {
                throw new InvalidOperationException(
                    $"{sectionName} is enabled, but Mobile, Password and FullName are not all configured. No user was created.");
            }

            if (!IranMobile.TryNormalize(mobile, out var normalizedMobile))
            {
                throw new InvalidOperationException(
                    $"{sectionName}:Mobile is invalid. No user was created.");
            }

            var normalizedFullName = fullName.Trim();
            if (normalizedFullName.Length > 200)
            {
                throw new InvalidOperationException(
                    $"{sectionName}:FullName exceeds the supported length. No user was created.");
            }

            if (password.Length < 12 || Encoding.UTF8.GetByteCount(password) > 72)
            {
                throw new InvalidOperationException(
                    $"{sectionName}:Password must be at least 12 characters and at most 72 UTF-8 bytes. No user was created.");
            }

            return new BootstrapCredentials(normalizedMobile, password, normalizedFullName);
        }

        private static User CreateUser(BootstrapCredentials credentials)
        {
            return new User
            {
                Id = Guid.NewGuid(),
                FullName = credentials.FullName,
                Mobile = credentials.Mobile,
                PasswordHash = PasswordHasher.Hash(credentials.Password),
                Status = (byte)Vitorize.Shared.Enums.UserStatus.Active,
                VerificationStatus = (byte)Vitorize.Shared.Enums.VerificationStatus.Pending,
                IsMobileConfirmed = true,
                IsEmailConfirmed = false,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };
        }

        private async Task SeedRolesAsync(CancellationToken cancellationToken)
        {
            var roles = new[]
            {
                ("SuperAdmin", "مدیر کل"),
                ("Admin", "مدیر فروشگاه"),
                ("KycViewer", "ناظر احراز هویت"),
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

                // ───────────── Order total KYC ─────────────
                S(OrderKycSettings.Keys.ThresholdToman, OrderKycSettings.DefaultThresholdToman.ToString(System.Globalization.CultureInfo.InvariantCulture), "Verification", "decimal", "آستانه مبلغ نهایی سفارش برای احراز هویت (تومان؛ صفر = غیرفعال)"),
                S(OrderKycSettings.Keys.CustomerNotice, OrderKycSettings.DefaultCustomerNotice, "Verification", "string", "متن راهنمای قابل جمع شدن در فرم احراز هویت سفارش"),

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
                S("StorefrontPersianFont", "Peyda", "Typography", "font", "فونت پیش‌فرض فارسی فروشگاه"),
                S("StorefrontEnglishFont", "Funnel Display", "Typography", "font", "فونت پیش‌فرض انگلیسی فروشگاه"),

                // ───────────── Trust seals (official provider snippets) ─────────────
                S("TrustSeal.FooterHtml", "", "TrustSeals", "trustedhtml", "کدهای رسمی نمادهای اعتماد در فوتر (اینماد، زرین‌پال، ایمالز، ترب و ...)"),

                // ───────────── SEO ─────────────
                S("MetaTitle", "ویتورایز | بازارگاه دیجیتال گیمینگ و خدمات آنلاین", "SEO", "string", "عنوان متای پیش‌فرض"),
                S("MetaDescription", "خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.", "SEO", "string", "توضیح متای پیش‌فرض"),
                S("MetaKeywords", "گیفت کارت, اشتراک, خدمات دیجیتال, بازی, گیمینگ, ویتورایز", "SEO", "string", "کلمات کلیدی پیش‌فرض"),
                S("SeoTitleTemplate", "{page} | {site}", "SEO", "string", "قالب عنوان صفحات ({page} و {site})"),
                S("Seo.CanonicalBaseUrl", "", "SEO", "string", "آدرس پایه HTTPS و میزبان اصلی برای canonical، robots و sitemap"),
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
                // Off by default, and deliberately seeded rather than hard-coded: the section is
                // hidden until an administrator turns it on, and because the seeder only inserts
                // missing keys, a later admin choice is never overwritten by a redeploy.
                S("HomePopularProductsEnabled", "false", "Homepage", "bool", "نمایش محبوب‌ترین کالاها در صفحه اصلی"),

                // The storefront's default product ordering. Public so the customer's sort menu can
                // show which order it is currently in. Seeded like every other key - inserted only
                // when absent - so an administrator's choice survives a redeploy, and code falls back
                // to the same value anyway if an older database has never seen this key.
                S("StorefrontDefaultProductSort", "AvailabilityFirst", "General", "sortmode",
                  "ترتیب پیش‌فرض نمایش کالاها برای مشتریان در فروشگاه"),

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
                  "[{\"icon\":\"layout-grid\",\"title\":\"انتخاب محصول\",\"text\":\"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن.\"},{\"icon\":\"credit-card\",\"title\":\"پرداخت امن\",\"text\":\"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده.\"},{\"icon\":\"zap\",\"title\":\"تحویل آنی\",\"text\":\"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود.\"}]",
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
                S("CustomHeadHtml", "", "Scripts", "trustedhtml", "کد سفارشی داخل <head> (فقط کد مورداعتماد مانند تحلیل و تگ‌ها)"),
                S("CustomFooterHtml", "", "Scripts", "trustedhtml", "کد سفارشی انتهای سایت (فقط کد مورداعتماد)"),

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
                S("ZarinpalMerchantId", Guid.Empty.ToString(), "Payment", "string", "شناسه پذیرنده زرین‌پال (مقدار نصب اولیه؛ پیش از پذیرش پرداخت باید با شناسه واقعی جایگزین شود)"),
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

        private sealed record BootstrapCredentials(string Mobile, string Password, string FullName);
    }
}
