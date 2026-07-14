# مدیریت و تاریخچه پیامک ویتورایز

## دامنه نهایی

سامانه فقط دو قرارداد SMS.ir دارد:

- OTP: پارامترهای `CODE` و `EXPIRE`
- اعلان: فقط پارامتر `ORDER_NUMBER`

رویدادهای خودکار فعال عبارت‌اند از Login OTP، Mobile Verification OTP، Forgot Password OTP، Order Paid، Gift Code Delivered، Ticket Reply، Verification Approved/Rejected و Wallet Top-Up Success. سیاست بسته‌ی `SmsAutomaticEventPolicy` از صف‌شدن سایر رویدادهای خودکار جلوگیری می‌کند.

رویدادهای Order Created/Completed/Cancelled/Status Changed، Ticket Closed/Reopened، Wallet Transaction، شارژ/برداشت دستی، بدهکارکردن کیف پول و 2FA OTP پیامک خودکار ندارند. اعلان‌های درون‌برنامه‌ای آن‌ها همچنان مستقل از SMS باقی می‌ماند.

## صفحه مدیر

مسیر `پنل مدیر ← مدیریت پیامک` (`/admin/sms`) امکانات زیر را ارائه می‌کند:

- سلامت اتصال، اعتبار، خطوط، دو شناسه قالب و وضعیت Outbox
- شمارنده‌های روز، جستجو و فیلتر بر اساس وضعیت، نوع، رویداد و تاریخ
- تاریخچه responsive، جزئیات پیام، تلاش‌ها، خطای امن ارائه‌دهنده و کد پیگیری
- بازتلاش فقط برای خطاهای گذرای Network، Timeout، ProviderUnavailable و TooManyRequests
- لغو پیام‌های Pending/Retrying و خروجی CSV ماسک‌شده
- پنل تست مستقل برای OTP و اعلان
- ارسال سفارشی قالب اعلان یا متن آزاد، که در حالت پیش‌فرض غیرفعال و صف‌محور است

شماره موبایل در خروجی و UI به شکل `0912***4567` نمایش داده می‌شود. مشاهده کامل فقط برای نقش SuperAdmin، با درخواست صریح و فعال‌بودن تنظیم `Sms.AllowAdminViewFullMobile` ممکن است. API key هیچ‌گاه در API مدیریت SMS بازگردانده نمی‌شود.

## تاریخچه و امنیت داده

`SmsMessages` یک رکورد پایدار برای هر درخواست ارسال و `SmsMessageAttempts` یک رکورد برای هر تلاش Outbox نگه می‌دارد. شناسه idempotency یکتا از ارسال تکراری یک رخداد جلوگیری می‌کند. رکورد شامل وضعیت، زمان‌ها، شناسه امن ارائه‌دهنده، کد خطای طبقه‌بندی‌شده، هزینه، مرجع عمومی و ارتباط با موجودیت است.

کد OTP ذخیره یا در preview ثبت نمی‌شود. اعلان قالبی فقط مرجع عمومی را نگه می‌دارد و هیچ مبلغ، موجودی، کد هدیه، دلیل احراز هویت، رمز، ایمیل یا شناسه داخلی وارد SMS نمی‌شود. ارسال متن سفارشی با feature flag، اعتبارسنجی موبایل/طول/نویسه، rate limit، تایید UI، idempotency و SecurityLog محافظت می‌شود.

## وضعیت‌ها و بازتلاش

وضعیت‌ها: Pending، Processing، Sent، Failed، Retrying، DeadLetter، Disabled و Cancelled. worker صف تلاش‌ها و نتیجه ارائه‌دهنده را اتمیک در تاریخچه به‌روزرسانی می‌کند. OTPهای مستقیم در تاریخچه ثبت می‌شوند، اما چون کد ذخیره نمی‌شود بازتلاش دستی ندارند.

payload جدید اعلان فقط `ORDER_NUMBER` دارد. worker برای payloadهای قدیمی `REFERENCE` را به `ORDER_NUMBER` تبدیل و `TITLE`/`DETAIL` را حذف می‌کند؛ بنابراین پیام‌های قدیمی Outbox شکسته نمی‌شوند.

## نصب Database-First

EF migration ایجاد نشده است. ترتیب اجرای SQL Server:

1. `Database/2026-07-13_create_sms_history.sql`
2. `Database/2026-07-13_seed_sms_settings.sql`
3. مقداردهی دو شناسه قالب و API key از تنظیمات مدیر

اسکریپت اول جدول‌ها، کلیدهای خارجی، ایندکس‌های فیلتر/جستجو، کلید یکتای idempotency و `dbo.usp_PurgeSmsHistory` را ایجاد می‌کند. procedure پاک‌سازی فقط رکوردهای terminal قدیمی را در batch حذف می‌کند و Pending/Processing/Retrying را دست نمی‌زند. یک SQL Agent job یا عملیات زمان‌بندی‌شده‌ی استقرار باید cutoff را مطابق `Sms.HistoryRetentionDays` (پیش‌فرض ۱۸۰ روز) به procedure بدهد؛ خود برنامه پاک‌سازی خودکار اجرا نمی‌کند.

## تنظیمات مدیریتی

تنظیمات جدید با پیش‌فرض امن seed می‌شوند:

- `Sms.CustomSendEnabled=false`
- `Sms.CustomTextEnabled=false`
- `Sms.MaxCustomRecipients=1`
- `Sms.MaxCustomTextLength=500`
- `Sms.RequireConfirmation=true`
- `Sms.AllowImmediateSend=false`
- `Sms.HistoryRetentionDays=180`
- `Sms.MaskMobileInAdmin=true`
- `Sms.AllowAdminViewFullMobile=false`
- `Sms.AllowRetryFailed=true`

شناسه‌های واقعی SMS.ir هاردکد نشده‌اند. دو ورودی اصلی `Sms.OtpTemplateId` و `Sms.NotificationTemplateId` کلیدهای قدیمی را برای سازگاری همگام می‌کنند.

## محدودیت عملیاتی

- پاک‌سازی history نیازمند زمان‌بندی procedure توسط اپراتور پایگاه‌داده است.
- ارسال سفارشی فعلاً عمداً تک‌گیرنده است؛ ارسال گروهی پیاده‌سازی نشده است.
- سطح مجوز موجود پروژه `AdminOnly`/`SuperAdmin` است و permission engine ریزدانه‌ای برای SMS وجود ندارد. قابلیت‌های پرخطر علاوه بر نقش با feature flag غیرفعال‌اند.
- Outboxهای بسیار قدیمی که پیش از ایجاد `SmsMessageId` ذخیره شده‌اند همچنان ارسال می‌شوند، اما اگر رکورد history متناظر نداشته باشند گذشته‌نگاری خودکار برای آن‌ها ساخته نمی‌شود.
