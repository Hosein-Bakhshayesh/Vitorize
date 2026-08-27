namespace Vitorize.Application.Common;

/// <summary>
/// Customer-facing transactional SMS copy. These messages are sent as plain text through the
/// durable SMS outbox, so they do not inherit the generic SMS.ir notification template.
/// </summary>
public static class OrderSmsMessages
{
    public static string Processing(string orderNumber) =>
        $"سفارش شما با موفقیت ثبت شد و اکنون در حال آماده‌سازی است.\nشماره سفارش: {orderNumber}\n\nبا تشکر، ویتورایز\nvitorize.com";

    public static string Completed(string orderNumber) =>
        $"سفارش شما با موفقیت تکمیل شد.\nشماره سفارش: {orderNumber}\n\nبا تشکر، ویتورایز\nvitorize.com";

    public static string Cancelled(string orderNumber) =>
        $"سفارش شما لغو شد.\nشماره سفارش: {orderNumber}\n\nبا تشکر، ویتورایز\nvitorize.com";
}
