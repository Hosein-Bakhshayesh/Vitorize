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

        public const string OrderPaid = "OrderPaid";
        public const string OrderCompleted = "OrderCompleted";
        public const string OrderStatusChanged = "OrderStatusChanged";
        public const string GiftCodeDelivered = "GiftCodeDelivered";
        public const string TicketReply = "TicketReply";
        public const string VerificationApproved = "VerificationApproved";
        public const string VerificationRejected = "VerificationRejected";
        public const string WalletTopUpSuccess = "WalletTopUpSuccess";
    }

    /// <summary>نام پارامترهای قالب SMS.ir؛ باید دقیقاً با متغیرهای تعریف‌شده در پنل مطابق باشد.</summary>
    public static class SmsTemplateParams
    {
        public const string Code = "CODE";
        public const string Expire = "EXPIRE";
        public const string Order = "ORDER";
        public const string Amount = "AMOUNT";
        public const string Balance = "BALANCE";
        public const string Ticket = "TICKET";
        public const string Name = "NAME";
        public const string Reason = "REASON";
        public const string Status = "STATUS";
    }
}
