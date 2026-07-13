# قالب‌های پیامک SMS.ir در ویتورایز

هر قالب باید در پنل SMS.ir ساخته و **تایید** شود. نام پارامترها باید **دقیقاً** با ستون
«نام پارامتر SMS.ir» زیر مطابق باشد (حساس به بزرگی/کوچکی حروف). شناسه‌ی هر قالب در تنظیم
مربوطه ذخیره می‌شود؛ **هیچ شناسه‌ای در کد هارد‌کد نمی‌شود**.

قالب‌های SMS.ir از نگه‌دارنده‌ی `#PARAM#` استفاده می‌کنند (مثال: `#CODE#`).

| کلید منطقی | کلید تنظیمات (Template Id) | پارامترهای SMS.ir | نمونه متن قالب فارسی | محل فراخوانی |
|---|---|---|---|---|
| `LoginOtp` | `Sms.LoginOtpTemplateId` | `CODE`, `EXPIRE` | `کد ورود شما: #CODE# (تا #EXPIRE# دقیقه معتبر)` | `AuthService.RequestLoginOtpAsync` |
| `RegisterOtp` | `Sms.RegisterOtpTemplateId` | `CODE`, `EXPIRE` | `کد تایید ثبت‌نام: #CODE# (تا #EXPIRE# دقیقه)` | `AuthService.SendOtpAsync` (MobileVerification) |
| `ForgotPassword` | `Sms.ForgotPasswordTemplateId` | `CODE`, `EXPIRE` | `کد بازیابی رمز: #CODE# (تا #EXPIRE# دقیقه)` | `AuthService.ForgotPasswordAsync` |
| `Otp` (عمومی/جایگزین) | `Sms.OtpTemplateId` | `CODE`, `EXPIRE` | `کد تایید: #CODE#` | جایگزین وقتی قالب اختصاصی تنظیم نشده |
| `OrderPaid` | `Sms.OrderPaidTemplateId` | `ORDER`, `AMOUNT` | `پرداخت سفارش #ORDER# به مبلغ #AMOUNT# تومان انجام شد.` | `PaymentService.CompletePaidOrderAsync` |
| `OrderCompleted` | `Sms.OrderCompletedTemplateId` | `ORDER` | `سفارش #ORDER# تکمیل شد.` | `OrderService.CompleteOrderAsync` |
| `OrderStatusChanged` | `Sms.OrderStatusChangedTemplateId` | `ORDER`, `STATUS` | `وضعیت سفارش #ORDER#: #STATUS#` | (رزرو برای تغییر وضعیت دستی) |
| `GiftCodeDelivered` | `Sms.GiftCodeDeliveredTemplateId` | `ORDER` | `کدهای سفارش #ORDER# در پنل شما آماده است.` | `PaymentService.CompletePaidOrderAsync` |
| `TicketReply` | `Sms.TicketReplyTemplateId` | `TICKET` | `به تیکت «#TICKET#» پاسخ داده شد.` | `TicketService.AdminAddMessageAsync` |
| `VerificationApproved` | `Sms.VerificationApprovedTemplateId` | `NAME` | `#NAME# عزیز، احراز هویت شما تایید شد.` | `VerificationService.ReviewAsync` |
| `VerificationRejected` | `Sms.VerificationRejectedTemplateId` | `NAME`, `REASON` | `#NAME# عزیز، احراز هویت رد شد. علت: #REASON#` | `VerificationService.ReviewAsync` |
| `WalletTopUpSuccess` | `Sms.WalletTopUpSuccessTemplateId` | `AMOUNT`, `BALANCE` | `کیف پول شما #AMOUNT# تومان شارژ شد. موجودی: #BALANCE#` | `WalletTopUpService.CreditWalletAsync` |

## نکات

- **مقدار پارامتر `EXPIRE`** برابر `Sms.OtpExpiryMinutes` (دقیقه) است.
- **مبالغ** (`AMOUNT`, `BALANCE`) با جداکننده هزارگان و بدون واحد ارسال می‌شوند؛ واحد را در متن قالب بگذارید.
- برای رویدادهای تجاری، اگر شناسه‌ی قالب مربوطه تنظیم نشده باشد، پیام با
  `SmsFailureReason.InvalidTemplate` در Outbox به‌صورت «شکست دائمی» ثبت می‌شود و تراکنش تجاری
  هیچ اثری نمی‌گیرد (بدون بازتلاش تا اصلاح تنظیمات).
- کد یکبار‌مصرف اگر قالب اختصاصی نداشته باشد، به قالب عمومی `Sms.OtpTemplateId` بازمی‌گردد.

## جریان ارسال

- **کد یکبار‌مصرف (ورود/ثبت‌نام/بازیابی):** هم‌زمان (synchronous) از طریق `ISmsService.SendOtpAsync`
  چون ارسال پیامک خودِ عملیات اصلی است. شکست ارسال، خطای امن فارسی به کاربر برمی‌گرداند.
- **رویدادهای تجاری:** ناهم‌زمان از طریق `ISmsOutboxEnqueuer` → `OutboxMessages` →
  `OutboxProcessorBackgroundService`. شکست ارائه‌دهنده هرگز پرداخت/تحویل/تیکت/احراز هویت را
  برنمی‌گرداند. بازتلاش با backoff نمایی و dead-letter پس از سقف تلاش.
