-- ============================================================================
-- Vitorize — Settings seed for the Final UI/UX Customization pass (2026-07-08)
-- NO SCHEMA CHANGES. Data-only. Idempotent — safe to run repeatedly.
--
-- Inserts every branding / SEO / homepage / footer / social / contact /
-- empty-state / error-page / logo & image Setting key used by the storefront
-- and admin panel. EXISTING ROWS ARE NEVER TOUCHED (values are preserved).
--
-- This mirrors VitorizeSeedService.SeedSettingsAsync, which also runs at API
-- startup. Applying this script is OPTIONAL — it only lets you provision the
-- keys without an app restart. Empty image/logo values fall back to built-in
-- defaults (e.g. the packaged mascot illustration).
-- ============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

;WITH seed([Key], [Value], GroupName, ValueType, [Description]) AS (
    SELECT * FROM (VALUES
        -- General
        (N'MaintenanceMode', N'false', N'General', N'bool', N'حالت تعمیر و نگهداری'),
        (N'MaintenanceMessage', N'به‌زودی با نسخه‌ای بهتر برمی‌گردیم. از صبوری شما سپاسگزاریم.', N'General', N'string', N'پیام صفحه حالت تعمیر'),
        -- Branding
        (N'BrandPrimaryColor', N'', N'Branding', N'color', N'رنگ اصلی برند'),
        -- Logos & Images
        (N'LogoPath', N'', N'Logos', N'image', N'لوگوی اصلی (تم روشن)'),
        (N'LogoDarkPath', N'', N'Logos', N'image', N'لوگوی تم تیره'),
        (N'LogoSmallPath', N'', N'Logos', N'image', N'لوگوی کوچک / آیکون'),
        (N'HeaderLogoPath', N'', N'Logos', N'image', N'لوگوی هدر'),
        (N'FooterLogoPath', N'', N'Logos', N'image', N'لوگوی فوتر'),
        (N'FaviconPath', N'', N'Logos', N'image', N'فاوآیکون سایت'),
        (N'AppleTouchIconPath', N'', N'Logos', N'image', N'آیکون Apple Touch'),
        (N'OgImagePath', N'', N'Logos', N'image', N'تصویر OpenGraph'),
        (N'TwitterImagePath', N'', N'Logos', N'image', N'تصویر توییتر / X'),
        (N'SocialPreviewImagePath', N'', N'Logos', N'image', N'تصویر پیش‌نمایش شبکه‌های اجتماعی'),
        (N'HeroBackgroundPath', N'', N'Logos', N'image', N'پس‌زمینه Hero'),
        (N'Error404IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۴۰۴'),
        (N'Error500IllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه ۵۰۰'),
        (N'MaintenanceIllustrationPath', N'', N'Logos', N'image', N'تصویر صفحه تعمیر'),
        (N'EmptyStateIllustrationPath', N'', N'Logos', N'image', N'تصویر پیش‌فرض حالت خالی'),
        -- SEO
        (N'MetaTitle', N'ویتورایز | بازارگاه دیجیتال گیمینگ و خدمات آنلاین', N'SEO', N'string', N'عنوان متای پیش‌فرض'),
        (N'MetaDescription', N'خرید سریع، مطمئن و رسمی گیفت کارت، اشتراک و خدمات دیجیتال با تحویل آنی و پشتیبانی ۲۴ ساعته.', N'SEO', N'string', N'توضیح متای پیش‌فرض'),
        (N'MetaKeywords', N'گیفت کارت, اشتراک, خدمات دیجیتال, بازی, گیمینگ, ویتورایز', N'SEO', N'string', N'کلمات کلیدی'),
        (N'SeoTitleTemplate', N'{page} | {site}', N'SEO', N'string', N'قالب عنوان صفحات'),
        (N'GoogleAnalyticsId', N'', N'SEO', N'string', N'شناسه Google Analytics'),
        -- Homepage
        (N'HeroCtaUrl', N'/shop', N'Homepage', N'string', N'لینک دکمه اصلی Hero'),
        (N'HeroSecondaryCtaText', N'دسته‌بندی‌ها', N'Homepage', N'string', N'متن دکمه دوم Hero'),
        (N'HeroSecondaryCtaUrl', N'/categories', N'Homepage', N'string', N'لینک دکمه دوم Hero'),
        (N'NewsletterTitle', N'از جدیدترین‌ها باخبر شو', N'Homepage', N'string', N'عنوان خبرنامه'),
        (N'NewsletterSubtitle', N'با عضویت در خبرنامه، از تخفیف‌ها و محصولات تازه زودتر از همه مطلع شو.', N'Homepage', N'string', N'زیرعنوان خبرنامه'),
        (N'NewsletterCtaText', N'عضویت', N'Homepage', N'string', N'متن دکمه خبرنامه'),
        (N'NewsletterPlaceholder', N'ایمیل خود را وارد کنید', N'Homepage', N'string', N'راهنمای ورودی خبرنامه'),
        -- About
        (N'AboutTitle', N'درباره ویتورایز', N'About', N'string', N'عنوان درباره ما'),
        (N'AboutText', N'ویتورایز بازارگاهی دیجیتال برای خرید امن و آنی گیفت کارت، اشتراک و خدمات آنلاین است.', N'About', N'string', N'متن درباره ما'),
        -- Trust badges & features (JSON)
        (N'TrustBadgesJson', N'[{"icon":"shield-check","title":"تضمین اصالت","text":"محصولات رسمی و اورجینال"},{"icon":"zap","title":"تحویل آنی","text":"سریع و بدون انتظار"},{"icon":"headphones","title":"پشتیبانی ۲۴/۷","text":"همیشه کنار شما"},{"icon":"lock","title":"پرداخت امن","text":"درگاه‌های معتبر"}]', N'Trust', N'json', N'نشان‌های اعتماد'),
        (N'HomeFeaturesKicker', N'چرا ویتورایز؟', N'Trust', N'string', N'برچسب بخش چرا ما'),
        (N'HomeFeaturesTitle', N'خرید دیجیتال، ساده و مطمئن', N'Trust', N'string', N'عنوان بخش چرا ما'),
        (N'HomeFeaturesJson', N'[{"icon":"grid","title":"انتخاب محصول","text":"از میان هزاران گیفت کارت، اشتراک و خدمت دیجیتال، محصول مورد نظرت را پیدا کن."},{"icon":"credit-card","title":"پرداخت امن","text":"با درگاه‌های معتبر بانکی یا کیف پول ویتورایز، پرداخت سریع و امن انجام بده."},{"icon":"zap","title":"تحویل آنی","text":"کد یا خدمت دیجیتال بلافاصله پس از پرداخت در حساب کاربری‌ات فعال می‌شود."}]', N'Trust', N'json', N'مراحل صفحه اول'),
        -- Footer
        (N'FooterText', N'', N'Footer', N'string', N'متن آزاد فوتر'),
        -- Social
        (N'WhatsAppUrl', N'', N'Social', N'string', N'واتساپ'),
        (N'XUrl', N'', N'Social', N'string', N'X (توییتر)'),
        (N'LinkedInUrl', N'', N'Social', N'string', N'لینکدین'),
        (N'DiscordUrl', N'', N'Social', N'string', N'دیسکورد'),
        (N'YouTubeUrl', N'', N'Social', N'string', N'یوتیوب'),
        (N'FacebookUrl', N'', N'Social', N'string', N'فیسبوک'),
        -- Contact
        (N'ContactAddress', N'', N'Contact', N'string', N'آدرس'),
        (N'WorkingHours', N'شنبه تا پنجشنبه، ۹ تا ۱۸', N'Contact', N'string', N'ساعات کاری'),
        -- Empty states
        (N'EmptyCartText', N'سبد خرید شما خالی است.', N'Empty', N'string', N'سبد خرید خالی'),
        (N'EmptyWishlistText', N'هنوز محصولی به علاقه‌مندی‌ها اضافه نکرده‌اید.', N'Empty', N'string', N'علاقه‌مندی خالی'),
        (N'EmptyOrdersText', N'هنوز سفارشی ثبت نکرده‌اید.', N'Empty', N'string', N'سفارش‌های خالی'),
        (N'EmptySearchText', N'نتیجه‌ای برای جستجوی شما پیدا نشد.', N'Empty', N'string', N'جستجوی بدون نتیجه'),
        (N'EmptyNotificationsText', N'اعلان جدیدی ندارید.', N'Empty', N'string', N'اعلان خالی'),
        (N'EmptyTicketsText', N'تیکتی ثبت نکرده‌اید.', N'Empty', N'string', N'تیکت خالی'),
        (N'EmptyReviewsText', N'هنوز نظری ثبت نشده است.', N'Empty', N'string', N'نظرات خالی'),
        (N'NoProductsText', N'محصولی برای نمایش وجود ندارد.', N'Empty', N'string', N'نبود محصول'),
        -- Error / status page texts
        (N'Error404Title', N'صفحه پیدا نشد', N'Errors', N'string', N'عنوان ۴۰۴'),
        (N'Error404Text', N'صفحه‌ای که دنبال آن هستید وجود ندارد یا منتقل شده است.', N'Errors', N'string', N'متن ۴۰۴'),
        (N'Error400Title', N'درخواست نامعتبر', N'Errors', N'string', N'عنوان ۴۰۰'),
        (N'Error400Text', N'درخواست شما معتبر نیست. لطفاً دوباره تلاش کنید.', N'Errors', N'string', N'متن ۴۰۰'),
        (N'Error401Title', N'نیاز به ورود', N'Errors', N'string', N'عنوان ۴۰۱'),
        (N'Error401Text', N'برای مشاهده این صفحه ابتدا وارد حساب کاربری شوید.', N'Errors', N'string', N'متن ۴۰۱'),
        (N'Error403Title', N'دسترسی مجاز نیست', N'Errors', N'string', N'عنوان ۴۰۳'),
        (N'Error403Text', N'شما اجازه دسترسی به این بخش را ندارید.', N'Errors', N'string', N'متن ۴۰۳'),
        (N'Error500Title', N'خطای غیرمنتظره', N'Errors', N'string', N'عنوان ۵۰۰'),
        (N'Error500Text', N'مشکلی در سرور رخ داد. تیم ما در حال بررسی است.', N'Errors', N'string', N'متن ۵۰۰'),
        (N'Error503Title', N'در حال به‌روزرسانی', N'Errors', N'string', N'عنوان ۵۰۳'),
        (N'Error503Text', N'سایت موقتاً در دسترس نیست. به‌زودی برمی‌گردیم.', N'Errors', N'string', N'متن ۵۰۳'),
        (N'SessionExpiredTitle', N'نشست شما منقضی شد', N'Errors', N'string', N'عنوان نشست منقضی'),
        (N'SessionExpiredText', N'برای ادامه دوباره وارد شوید.', N'Errors', N'string', N'متن نشست منقضی'),
        (N'NetworkErrorTitle', N'خطای ارتباط', N'Errors', N'string', N'عنوان خطای شبکه'),
        (N'NetworkErrorText', N'ارتباط با سرور برقرار نشد. اتصال اینترنت خود را بررسی کنید.', N'Errors', N'string', N'متن خطای شبکه'),
        (N'OfflineTitle', N'اتصال اینترنت قطع است', N'Errors', N'string', N'عنوان آفلاین'),
        (N'OfflineText', N'به نظر می‌رسد اینترنت شما قطع شده است.', N'Errors', N'string', N'متن آفلاین'),
        (N'PageRemovedTitle', N'این صفحه حذف شده است', N'Errors', N'string', N'عنوان صفحه حذف‌شده'),
        (N'PageRemovedText', N'محتوایی که دنبال آن بودید دیگر در دسترس نیست.', N'Errors', N'string', N'متن صفحه حذف‌شده'),
        -- Custom scripts
        (N'CustomHeadHtml', N'', N'Scripts', N'string', N'کد سفارشی <head>'),
        (N'CustomFooterHtml', N'', N'Scripts', N'string', N'کد سفارشی انتهای صفحه'),
        -- Email (SMTP)
        (N'SmtpHost', N'', N'Email', N'string', N'میزبان SMTP'),
        (N'SmtpPort', N'587', N'Email', N'int', N'پورت SMTP'),
        (N'SmtpUsername', N'', N'Email', N'string', N'نام کاربری SMTP'),
        (N'SmtpFromEmail', N'', N'Email', N'string', N'ایمیل فرستنده'),
        (N'SmtpFromName', N'ویتورایز', N'Email', N'string', N'نام فرستنده'),
        (N'SmtpEnableSsl', N'true', N'Email', N'bool', N'استفاده از SSL'),
        -- Security
        (N'RequireEmailConfirmation', N'false', N'Security', N'bool', N'الزام تأیید ایمیل'),
        (N'MinPasswordLength', N'8', N'Security', N'int', N'حداقل طول رمز'),
        (N'MaxLoginAttempts', N'5', N'Security', N'int', N'حداکثر تلاش ورود'),
        -- Uploads
        (N'MaxUploadSizeMb', N'2', N'Uploads', N'int', N'حداکثر حجم آپلود (مگابایت)'),
        (N'AllowedImageFormats', N'jpg,jpeg,png,webp', N'Uploads', N'string', N'فرمت‌های مجاز تصویر')
    ) AS v([Key], [Value], GroupName, ValueType, [Description])
)
INSERT INTO Settings (Id, [Key], [Value], GroupName, ValueType, [Description], UpdatedAt)
SELECT NEWID(), s.[Key], s.[Value], s.GroupName, s.ValueType, s.[Description], SYSUTCDATETIME()
FROM seed s
WHERE NOT EXISTS (SELECT 1 FROM Settings e WHERE e.[Key] = s.[Key]);

PRINT 'Vitorize UI/UX settings seed complete. Existing values were preserved.';
