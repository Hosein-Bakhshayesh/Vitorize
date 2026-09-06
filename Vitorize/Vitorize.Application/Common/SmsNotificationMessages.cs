namespace Vitorize.Application.Common;

/// <summary>
/// متن‌های ثابت پیامک‌های تراکنشی غیر OTP. این پیام‌ها با شماره مبدأ آسانک
/// و از صف پایدار ارسال می‌شوند و به قالب پیامک وابسته نیستند.
/// </summary>
public static class SmsNotificationMessages
{
    public const string Footer = "باتشکر، ویتورایز\nVitorize.com";

    public static string WithFooter(string message) => $"{message.TrimEnd()}\n\n{Footer}";

    /// <summary>فوتر استاندارد را بدون تکرار به هر پیام متنی اضافه می‌کند.</summary>
    public static string EnsureFooter(string message)
    {
        var normalized = message.TrimEnd();
        return normalized.EndsWith(Footer, StringComparison.Ordinal)
            ? normalized
            : WithFooter(normalized);
    }

    public static string Otp(string code, int expiryMinutes) =>
        WithFooter($"کد تایید شما در ویتورایز:\n{code}\nاعتبار: {expiryMinutes} دقیقه\nاین کد را در اختیار دیگران قرار ندهید.");

    public static string WalletTopUpSucceeded(decimal amount, string reference) =>
        WithFooter($"شارژ کیف پول شما با موفقیت انجام شد.\nمبلغ: {amount:#,0} تومان\nکد پیگیری: {reference}");

    public static string TicketReply(string reference) =>
        WithFooter($"پاسخ جدیدی برای تیکت شما ثبت شد.\nکد پیگیری: {reference}\n\nبرای مشاهده پاسخ وارد حساب کاربری شوید.");

    public static string AdminReferenceNotification(string reference) =>
        WithFooter($"اطلاع‌رسانی جدیدی در حساب کاربری شما ثبت شد.\nکد پیگیری: {reference}\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید.");

    public static string LegacyNotification(string templateKey, string reference) => templateKey switch
    {
        SmsTemplateKeys.TicketReply => TicketReply(reference),
        SmsTemplateKeys.WalletTopUpSuccess =>
            WithFooter($"شارژ کیف پول شما با موفقیت انجام شد.\nکد پیگیری: {reference}"),
        SmsTemplateKeys.VerificationApproved =>
            WithFooter("احراز هویت شما با موفقیت تایید شد."),
        SmsTemplateKeys.VerificationRejected =>
            WithFooter("درخواست احراز هویت شما رد شد.\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید."),
        SmsTemplateKeys.GiftCodeDelivered =>
            WithFooter($"اطلاعات سفارش شما آماده است.\nکد پیگیری: {reference}\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید."),
        SmsTemplateKeys.OrderCompleted => OrderSmsMessages.Completed(reference),
        SmsTemplateKeys.OrderCancelled => OrderSmsMessages.Cancelled(reference),
        SmsTemplateKeys.OrderPaid or SmsTemplateKeys.OrderCreated or SmsTemplateKeys.OrderStatusChanged =>
            OrderSmsMessages.Processing(reference),
        _ => AdminReferenceNotification(reference)
    };
}
