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

**بازبینی نهایی ۲۰۲۶/۰۷/۰۸ هیچ تغییر Schema جدیدی لازم نکرد.** نقش‌ها، کاربران پیش‌فرض و
تنظیمات (از جمله کلیدهای جدید گروه Branding) توسط Seeder داخلی API در اولین اجرا به‌صورت
Idempotent ساخته می‌شوند و اسکریپت دستی نمی‌خواهند.

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

### 7) حساب‌های پیش‌فرض (Seeder)
- ادمین: `09123456789 / 12345678` — **بلافاصله بعد از استقرار رمز عبور را عوض کنید.**
- مشتری نمونه: `09378149896 / 123456` — در Production در صورت عدم نیاز غیرفعال شود.
