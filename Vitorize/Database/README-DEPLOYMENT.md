# Vitorize — Manual Deployment Guide (Database-First)

این پروژه Database-First است و **هیچ EF Migration ندارد**. تغییرات دیتابیس فقط از طریق
اسکریپت‌های SQL این پوشه اعمال می‌شوند.

## اسکریپت‌های SQL و ترتیب اجرا

| # | فایل | نوع | وضعیت |
|---|------|-----|--------|
| 1 | `2026-07-07_fix_GiftCodeReservations_Status_constraint.sql` | Schema (Constraint) | اگر قبلاً اجرا نشده، اجرا شود |
| 2 | `2026-07-08_data_fix_image_paths_and_names.sql` | Data-only، Idempotent | روی دیتابیس Development اعمال شده؛ روی هر محیط دیگری که همین داده‌ها را دارد اجرا شود |
| 3 | `2026-07-14_product_experience_schema.sql` | Schema، Idempotent | پیش از انتشار نسخه Product Experience اجرا شود |
| 4 | `2026-07-14_seed_product_experience_settings.sql` | Settings seed، Idempotent | بعد از اسکریپت شماره ۳؛ مقادیر موجود را بازنویسی نمی‌کند |
| — | `2026-07-14_optional_normalize_legacy_lucide_icons.sql` | Data-only، اختیاری و Idempotent | فقط برای پاک‌سازی نام‌های قدیمی آیکون؛ اجرای آن برای کارکرد برنامه الزامی نیست |

فایل‌ها UTF-8 هستند. آن‌ها را با SSMS/Azure Data Studio یا با `sqlcmd -f 65001` اجرا کنید تا متن‌های فارسی تنظیمات بدون تغییر کدگذاری ثبت شوند.

نقش‌ها و تنظیمات غیرمحرمانه توسط Seeder داخلی API به‌صورت Idempotent ساخته می‌شوند.
Seeder در حالت پیش‌فرض هیچ حساب کاربری ایجاد یا بازنویسی نمی‌کند.

## گام‌های استقرار

### 1) دیتابیس
- دیتابیس `VitorizeDb` را از محیط فعلی Restore/ایجاد کنید (Database-First).
- اسکریپت‌های جدول بالا را به ترتیب اجرا کنید.

### 2) فایل‌های آپلودشده (مهم — ریشه‌ی مشکل تصاویر)
مرجع فایل‌های آپلودی، **wwwroot میزبان API** است:

```
Vitorize.Api\wwwroot\uploads\{products|categories|brands|banners|settings|verifications}
```

- اگر از محیط قبلی فایل آپلودی دارید، همه را از `Vitorize.Web\wwwroot\uploads\*`
  به `Vitorize.Api\wwwroot\uploads\*` منتقل کنید (در مخزن، نسخه‌ی Development این کار
  انجام شده است). پوشه‌ی `uploads` داخل Web دیگر استفاده نمی‌شود و می‌تواند حذف شود.
- در استقرار، پوشه‌ی `wwwroot\uploads` سرور API باید **writable** باشد (Application Pool
  Identity در IIS یا کاربر سرویس در Linux باید مجوز نوشتن داشته باشد).
- برای بقای فایل‌ها بین دیپلوی‌ها، این پوشه را خارج از پکیج انتشار نگه دارید
  (مثلاً junction/symlink یا تنظیم مسیر اشتراکی).

### 3) پیکربندی API (`Vitorize.Api\appsettings.Production.json` یا Env Vars)
- `ConnectionStrings:DefaultConnection` → کانکشن Production.
- `Jwt:SecretKey` → **حتماً عوض شود** (مقدار داخل مخزن فقط برای توسعه است). حداقل ۳۲ کاراکتر تصادفی.
- `Encryption:Key` → **حتماً عوض شود** (کلید رمزنگاری کدهای گیفت؛ ۳۲ بایت). توجه: تغییر این
  کلید پس از ثبت کد در دیتابیس، کدهای قبلی را غیرقابل رمزگشایی می‌کند — قبل از ورود داده واقعی نهایی شود.
- `ASPNETCORE_ENVIRONMENT=Production` → مسیرهای Mock (پرداخت تستی و شارژ تستی کیف پول)
  به‌صورت خودکار 404 می‌شوند.

### 4) پیکربندی Web (`Vitorize.Web\appsettings.Production.json`)
- `ApiSettings:BaseUrl` → مثل `https://api.vitorize.com/api/`
- `ApiSettings:MediaBaseUrl` → مثل `https://api.vitorize.com` (بدون اسلش انتهایی؛ تصاویر از این میزبان لود می‌شوند)

### 5) تنظیمات داخل پنل ادمین (پس از اولین اجرا)
در «تنظیمات» پنل ادمین مقادیر زیر را برای Production به‌روز کنید:
- گروه Payment: `ZarinpalMerchantId` (شناسه واقعی)، `ZarinpalSandbox=false`،
  `ZarinpalBaseUrl=https://payment.zarinpal.com/pg/v4/payment`،
  `ZarinpalStartPayUrl=https://payment.zarinpal.com/pg/StartPay`،
  `ZarinpalCallbackUrl=https://api.<domain>/api/payments/zarinpal/callback`
- گروه Branding: متن‌های Hero، فوتر، کپی‌رایت، نام سایت و ... (بدون تغییر کد قابل ویرایش‌اند)
- گروه Support/Social: تلفن، ایمیل، اینستاگرام، تلگرام

### 6) CORS
دامنه‌های Production فروشگاه در `Vitorize.Api\Program.cs` (سیاست `VitorizeCors`)
از قبل شامل `https://vitorize.com` و `https://www.vitorize.com` است؛ در صورت تفاوت دامنه، به‌روزرسانی شود.

### 7) راه‌اندازی امن اولین SuperAdmin

هیچ حساب پیش‌فرضی در کد یا فایل‌های `appsettings` وجود ندارد. اگر دیتابیس هنوز هیچ
SuperAdmin ندارد، مقادیر زیر را فقط از طریق Environment Variable یا Secret Provider
به API بدهید:

```text
BootstrapAdmin__Enabled=true
BootstrapAdmin__Mobile=<mobile-from-secret-provider>
BootstrapAdmin__Password=<strong-random-password>
BootstrapAdmin__FullName=<operator-name>
```

رمز باید حداقل ۱۲ کاراکتر و حداکثر ۷۲ بایت UTF-8 باشد. هنگام شروع API، حساب فقط زمانی
ایجاد می‌شود که فلگ فعال باشد، تمام مقادیر معتبر باشند و هیچ SuperAdmin دیگری در دیتابیس
وجود نداشته باشد. کاربر موجود با همان موبایل هرگز تغییر یا ارتقا داده نمی‌شود.

پس از مشاهده رویداد امنیتی `BootstrapSuperAdminCreated` و ورود موفق:

1. `BootstrapAdmin__Enabled` را به `false` تغییر دهید یا حذف کنید.
2. تمام متغیرهای `BootstrapAdmin__*` را از محیط اجرا/Secret Provider حذف کنید.
3. API را دوباره راه‌اندازی کنید و غیرفعال بودن Bootstrap را بررسی کنید.

مقادیر Bootstrap در دیتابیس یا لاگ نوشته نمی‌شوند و نباید در فایل‌های تنظیمات commit شوند.

### 8) کاربر نمایشی Development

ساخت کاربر نمایشی به‌صورت پیش‌فرض غیرفعال است و حتی با فلگ فعال در Staging/Production
نادیده گرفته می‌شود. برای نیاز موقت توسعه، فقط در محیط `Development` و از طریق User Secrets
یا Environment Variables استفاده کنید:

```text
DevelopmentDemoUser__Enabled=true
DevelopmentDemoUser__Mobile=<development-only-mobile>
DevelopmentDemoUser__Password=<development-only-password>
DevelopmentDemoUser__FullName=<development-only-name>
```

این مقادیر صرفاً برای محیط محلی Development هستند و نباید در فایل‌های commit‌شده یا محیط
Staging/Production قرار گیرند. ابزار Seed پنل نیز فقط در Development، فقط برای SuperAdmin
و فقط برای داده‌های مرجع غیرکاربری فعال است؛ اجرای آن در Security Logs ثبت می‌شود.

### 9) ارتقا از نسخه‌های قدیمی

پیش از انتشار، حساب‌های ایجادشده توسط Seeder قدیمی را در هر دیتابیس موجود بررسی کنید.
حساب مشکوک را پس از تأیید مالکیت غیرفعال/حذف و تمام Refresh Tokenهای آن را لغو کنید. این
نسخه عمداً حساب‌های موجود یا رمز آن‌ها را خودکار تغییر نمی‌دهد تا یک حساب واقعی قفل نشود.
