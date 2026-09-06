namespace Vitorize.Application.Common;

/// <summary>
/// متن‌های ثابت پیامک‌های تراکنشی غیر OTP. این پیام‌ها با شماره مبدأ آسانک
/// و از صف پایدار ارسال می‌شوند و به قالب پیامک وابسته نیستند.
/// </summary>
public static class SmsNotificationMessages
{
    public static string WalletTopUpSucceeded(decimal amount, string reference) =>
        $"شارژ کیف پول شما با موفقیت انجام شد.\nمبلغ: {amount:#,0} تومان\nکد پیگیری: {reference}\n\nبا تشکر، ویتورایز\nvitorize.com";

    public static string TicketReply(string reference) =>
        $"پاسخ جدیدی برای تیکت شما ثبت شد.\nکد پیگیری: {reference}\n\nبرای مشاهده پاسخ وارد حساب کاربری شوید.\nویتورایز\nvitorize.com";

    public static string AdminReferenceNotification(string reference) =>
        $"اطلاع‌رسانی جدیدی در حساب کاربری شما ثبت شد.\nکد پیگیری: {reference}\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید.\nویتورایز\nvitorize.com";

    public static string LegacyNotification(string templateKey, string reference) => templateKey switch
    {
        SmsTemplateKeys.TicketReply => TicketReply(reference),
        SmsTemplateKeys.WalletTopUpSuccess =>
            $"شارژ کیف پول شما با موفقیت انجام شد.\nکد پیگیری: {reference}\n\nبا تشکر، ویتورایز\nvitorize.com",
        SmsTemplateKeys.VerificationApproved =>
            "احراز هویت شما با موفقیت تایید شد.\n\nبا تشکر، ویتورایز\nvitorize.com",
        SmsTemplateKeys.VerificationRejected =>
            "درخواست احراز هویت شما رد شد.\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید.\nویتورایز\nvitorize.com",
        SmsTemplateKeys.GiftCodeDelivered =>
            $"اطلاعات سفارش شما آماده است.\nکد پیگیری: {reference}\n\nبرای مشاهده جزئیات وارد حساب کاربری شوید.\nویتورایز\nvitorize.com",
        SmsTemplateKeys.OrderCompleted => OrderSmsMessages.Completed(reference),
        SmsTemplateKeys.OrderCancelled => OrderSmsMessages.Cancelled(reference),
        SmsTemplateKeys.OrderPaid or SmsTemplateKeys.OrderCreated or SmsTemplateKeys.OrderStatusChanged =>
            OrderSmsMessages.Processing(reference),
        _ => AdminReferenceNotification(reference)
    };
}
