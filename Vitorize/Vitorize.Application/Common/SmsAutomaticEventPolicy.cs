namespace Vitorize.Application.Common
{
    /// <summary>
    /// مسیر قالبی پیامک حذف شده است؛ همهٔ ارسال‌های خودکار به متن آزاد Outbox می‌روند.
    /// </summary>
    public static class SmsAutomaticEventPolicy
    {
        public static readonly IReadOnlySet<string> AllowedOtpTemplates =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public static readonly IReadOnlySet<string> RemovedAutomaticTemplates = new HashSet<string>(
        [
            SmsTemplateKeys.OrderCreated,
            SmsTemplateKeys.OrderCompleted,
            SmsTemplateKeys.OrderStatusChanged,
            SmsTemplateKeys.OrderCancelled,
            SmsTemplateKeys.TicketClosed,
            SmsTemplateKeys.TicketReopened,
            SmsTemplateKeys.WalletTransaction
        ], StringComparer.OrdinalIgnoreCase);

        public static bool IsAllowedTemplate(string templateKey) =>
            AllowedOtpTemplates.Contains(templateKey);
    }
}
