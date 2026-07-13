namespace Vitorize.Application.Common
{
    /// <summary>
    /// کلیدهای تنظیمات پیامک در جدول Settings (گروه «SMS»).
    /// این کلیدها هرگز نباید از طریق endpoint عمومی تنظیمات برگردانده شوند.
    /// </summary>
    public static class SmsSettingKeys
    {
        public const string Group = "SMS";

        public const string IsEnabled = "Sms.IsEnabled";
        public const string Provider = "Sms.Provider";
        public const string ApiKey = "Sms.ApiKey";
        public const string DefaultLineNumber = "Sms.DefaultLineNumber";
        public const string SenderName = "Sms.SenderName";

        // Template ids
        public const string OtpTemplateId = "Sms.OtpTemplateId";
        public const string LoginOtpTemplateId = "Sms.LoginOtpTemplateId";
        public const string RegisterOtpTemplateId = "Sms.RegisterOtpTemplateId";
        public const string ForgotPasswordTemplateId = "Sms.ForgotPasswordTemplateId";
        public const string OrderPaidTemplateId = "Sms.OrderPaidTemplateId";
        public const string OrderCompletedTemplateId = "Sms.OrderCompletedTemplateId";
        public const string GiftCodeDeliveredTemplateId = "Sms.GiftCodeDeliveredTemplateId";
        public const string TicketReplyTemplateId = "Sms.TicketReplyTemplateId";
        public const string VerificationApprovedTemplateId = "Sms.VerificationApprovedTemplateId";
        public const string VerificationRejectedTemplateId = "Sms.VerificationRejectedTemplateId";
        public const string OrderStatusChangedTemplateId = "Sms.OrderStatusChangedTemplateId";
        public const string WalletTopUpSuccessTemplateId = "Sms.WalletTopUpSuccessTemplateId";

        // Reliability / OTP policy
        public const string MaxRetryCount = "Sms.MaxRetryCount";
        public const string RetryDelaySeconds = "Sms.RetryDelaySeconds";
        public const string OtpExpiryMinutes = "Sms.OtpExpiryMinutes";
        public const string OtpResendCooldownSeconds = "Sms.OtpResendCooldownSeconds";
        public const string OtpMaxAttempts = "Sms.OtpMaxAttempts";
        public const string DailyOtpLimitPerMobile = "Sms.DailyOtpLimitPerMobile";
        public const string DailySmsLimitPerMobile = "Sms.DailySmsLimitPerMobile";
        public const string LogSensitiveData = "Sms.LogSensitiveData";
        public const string UseOutbox = "Sms.UseOutbox";

        /// <summary>
        /// کلیدهای محرمانه یا داخلی که نباید در پاسخ عمومی/کلاینت آشکار شوند.
        /// </summary>
        public static readonly IReadOnlySet<string> SecretKeys =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                ApiKey,
                DefaultLineNumber
            };
    }
}
