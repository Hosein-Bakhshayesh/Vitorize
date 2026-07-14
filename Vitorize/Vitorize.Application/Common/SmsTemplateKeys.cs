namespace Vitorize.Application.Common
{
    /// <summary>
    /// شناسه‌های منطقی قالب‌های پیامک. هر کلید به یک کلید تنظیمات (Template Id) نگاشت می‌شود
    /// و شناسه واقعی قالب SMS.ir هرگز در کد هارد‌کد نمی‌شود.
    /// </summary>
    public static class SmsTemplateKeys
    {
        public const string LoginOtp = "LoginOtp";
        public const string RegisterOtp = "RegisterOtp";
        public const string ForgotPassword = "ForgotPassword";
        public const string GenericOtp = "Otp";
        public const string UniversalNotification = "Notification";

        public const string OrderCreated = "OrderCreated";
        public const string OrderPaid = "OrderPaid";
        public const string OrderCompleted = "OrderCompleted";
        public const string OrderStatusChanged = "OrderStatusChanged";
        public const string OrderCancelled = "OrderCancelled";
        public const string GiftCodeDelivered = "GiftCodeDelivered";
        public const string TicketReply = "TicketReply";
        public const string TicketClosed = "TicketClosed";
        public const string TicketReopened = "TicketReopened";
        public const string WalletTransaction = "WalletTransaction";
        public const string VerificationApproved = "VerificationApproved";
        public const string VerificationRejected = "VerificationRejected";
        public const string WalletTopUpSuccess = "WalletTopUpSuccess";

        public static readonly IReadOnlyList<string> OtpTemplates =
        [
            GenericOtp,
            LoginOtp,
            RegisterOtp,
            ForgotPassword
        ];

        public static readonly IReadOnlyList<string> NotificationTemplates =
        [
            UniversalNotification,
            OrderCreated,
            OrderPaid,
            OrderCompleted,
            OrderStatusChanged,
            OrderCancelled,
            GiftCodeDelivered,
            TicketReply,
            TicketClosed,
            TicketReopened,
            WalletTransaction,
            VerificationApproved,
            VerificationRejected,
            WalletTopUpSuccess
        ];

        public static bool IsOtp(string templateKey) =>
            OtpTemplates.Contains(templateKey, StringComparer.OrdinalIgnoreCase);

        public static bool IsNotification(string templateKey) =>
            NotificationTemplates.Contains(templateKey, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>نام پارامترهای قالب SMS.ir؛ باید دقیقاً با متغیرهای تعریف‌شده در پنل مطابق باشد.</summary>
    public static class SmsTemplateParams
    {
        public const string Code = "CODE";
        public const string Expire = "EXPIRE";
        public const string OrderNumber = "ORDER_NUMBER";
    }
}
