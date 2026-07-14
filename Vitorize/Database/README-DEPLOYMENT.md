# Vitorize — Manual Deployment Guide (Database-First)

این پروژه Database-First است و **هیچ EF Migration ندارد**. تغییرات دیتابیس فقط از طریق
اسکریپت‌های SQL این پوشه اعمال می‌شوند.

## زنجیره رسمی دیتابیس

منبع رسمی ترتیب اجرا، طبقه‌بندی، وابستگی و SHA-256 همه فایل‌ها در
[`DEPLOYMENT-MANIFEST.md`](DEPLOYMENT-MANIFEST.md) و فایل ماشین‌خوان
`deployment-manifest.json` قرار دارد. اسکریپت‌های تاریخی را در Production دستی و بر اساس
نام فایل اجرا نکنید؛ بعضی از آن‌ها اختیاری، وابسته به محیط یا با نسخه امن‌تر جایگزین شده‌اند.

زنجیره الزامی فعلی شامل هشت مرحله است: ایجاد Ledger، اصلاح اتمیک constraint رزرو گیفت،
schema تاریخچه SMS، schema تجربه محصول، seed نقش‌های غیرمحرمانه، و سه seed تنظیمات.
Runner هر فایل را قبل از اتصال با SHA-256 بررسی و فقط اجرای موفق را در
`dbo.DatabaseScriptHistory` ثبت می‌کند.

```powershell
# بررسی بدون تغییر
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Deploy-Database.ps1 `
  -ServerInstance <server> -Database <database> -Environment Production -DryRun

# اجرا؛ نام دیتابیس باید دقیقاً دوباره تأیید شود
powershell -NoProfile -ExecutionPolicy Bypass -File Database\Deploy-Database.ps1 `
  -ServerInstance <server> -Database <database> -Environment Production `
  -ConfirmDatabaseName <database>
```

پیش از اجرا Backup آزموده‌شده تهیه کنید و خروجی read-only فایل
`Preflight/validate_database_state.sql` را بررسی کنید. بعد از اجرا نیز Runner فایل
`PostDeploy/verify_database_deployment.sql` را اجرا می‌کند. فایل‌ها UTF-8 هستند و Runner
از `sqlcmd -f 65001` و Windows Integrated Authentication استفاده می‌کند؛ هیچ credential
دیتابیس در مخزن یا command line نگهداری نمی‌شود.

برای دیتابیس جدید، baseline بدون داده و بدون secret در
`Baseline/VitorizeDb.schema-candidate.dacpac` قرار دارد. قبل از Publish، checksum همراه آن
و محدودیت‌های ثبت‌شده در [`MODEL-SCRIPT-MISMATCHES.md`](MODEL-SCRIPT-MISMATCHES.md) را
بررسی کنید.

نقش‌ها و تنظیمات غیرمحرمانه توسط Seeder داخلی API نیز به‌صورت Idempotent بررسی می‌شوند؛
این مسیر fallback است و جایگزین زنجیره ثبت‌شده استقرار نیست. Seeder در حالت پیش‌فرض هیچ
حساب کاربری ایجاد یا بازنویسی نمی‌کند.

## گام‌های استقرار

### 1) دیتابیس
- برای محیط جدید baseline بازبینی‌شده را Publish و سپس Runner را اجرا کنید.
- برای محیط موجود Backup بگیرید، Preflight و Dry Run را اجرا کنید و سپس Runner را با
  تأیید دقیق نام دیتابیس اجرا کنید.
- ترتیب دستی قدیمی دیگر معتبر نیست؛ فقط Manifest رسمی مبنا است.

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
