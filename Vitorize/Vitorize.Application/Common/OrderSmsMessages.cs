namespace Vitorize.Application.Common;

/// <summary>
/// Customer-facing transactional SMS copy. These messages are sent as plain text through the
/// durable SMS outbox, so they do not inherit the generic Asanak notification template.
/// </summary>
public static class OrderSmsMessages
{
    public static string Processing(string orderNumber) =>
        SmsNotificationMessages.WithFooter($"سفارش شما با موفقیت ثبت شد و اکنون در حال آماده‌سازی است.\nشماره سفارش: {orderNumber}");

    public static string Completed(string orderNumber) =>
        SmsNotificationMessages.WithFooter($"سفارش شما با موفقیت تکمیل شد.\nشماره سفارش: {orderNumber}");

    public static string Cancelled(string orderNumber) =>
        SmsNotificationMessages.WithFooter($"سفارش شما لغو شد.\nشماره سفارش: {orderNumber}");
}
