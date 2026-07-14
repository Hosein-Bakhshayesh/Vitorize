namespace Vitorize.Application.Common
{
    /// <summary>
    /// فهرست مرکزی و بسته‌ی رویدادهایی که اجازه دارند به‌صورت خودکار پیامک بفرستند.
    /// کلیدهای قدیمی قالب برای سازگاری تنظیمات باقی مانده‌اند، اما حضور یک کلید در
    /// SmsTemplateKeys به معنی مجاز بودن ارسال خودکار آن نیست.
    /// </summary>
    public static class SmsAutomaticEventPolicy
    {
        public static readonly IReadOnlySet<string> AllowedOtpTemplates = new HashSet<string>(
        [
            SmsTemplateKeys.LoginOtp,
            SmsTemplateKeys.RegisterOtp,
            SmsTemplateKeys.ForgotPassword
        ], StringComparer.OrdinalIgnoreCase);

        public static readonly IReadOnlySet<string> AllowedNotificationTemplates = new HashSet<string>(
        [
            SmsTemplateKeys.OrderPaid,
            SmsTemplateKeys.GiftCodeDelivered,
            SmsTemplateKeys.TicketReply,
            SmsTemplateKeys.VerificationApproved,
            SmsTemplateKeys.VerificationRejected,
            SmsTemplateKeys.WalletTopUpSuccess
        ], StringComparer.OrdinalIgnoreCase);

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
            AllowedOtpTemplates.Contains(templateKey) ||
            AllowedNotificationTemplates.Contains(templateKey);
    }
}
