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
        public const string NotificationTemplateId = "Sms.NotificationTemplateId";
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

        /// <summary>
        /// کلید اصلی و کلیدهای قدیمی قالب یکپارچه OTP. تمام این کلیدها باید همواره مقدار یکسان داشته باشند.
        /// </summary>
        public static readonly IReadOnlyList<string> OtpTemplateIdKeys =
        [
            OtpTemplateId,
            LoginOtpTemplateId,
            RegisterOtpTemplateId,
            ForgotPasswordTemplateId
        ];

        /// <summary>
        /// کلید اصلی و کلیدهای قدیمی قالب یکپارچه اعلان تجاری. تمام این کلیدها باید همواره مقدار یکسان داشته باشند.
        /// </summary>
        public static readonly IReadOnlyList<string> NotificationTemplateIdKeys =
        [
            NotificationTemplateId,
            OrderPaidTemplateId,
            OrderCompletedTemplateId,
            OrderStatusChangedTemplateId,
            GiftCodeDeliveredTemplateId,
            TicketReplyTemplateId,
            VerificationApprovedTemplateId,
            VerificationRejectedTemplateId,
            WalletTopUpSuccessTemplateId
        ];

        public static bool TryGetTemplateIdGroup(string key, out IReadOnlyList<string> group)
        {
            if (OtpTemplateIdKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                group = OtpTemplateIdKeys;
                return true;
            }

            if (NotificationTemplateIdKeys.Contains(key, StringComparer.OrdinalIgnoreCase))
            {
                group = NotificationTemplateIdKeys;
                return true;
            }

            group = Array.Empty<string>();
            return false;
        }

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
        public const string CustomSendEnabled = "Sms.CustomSendEnabled";
        public const string CustomTextEnabled = "Sms.CustomTextEnabled";
        public const string MaxCustomRecipients = "Sms.MaxCustomRecipients";
        public const string MaxCustomTextLength = "Sms.MaxCustomTextLength";
        public const string RequireConfirmation = "Sms.RequireConfirmation";
        public const string AllowImmediateSend = "Sms.AllowImmediateSend";
        public const string HistoryRetentionDays = "Sms.HistoryRetentionDays";
        public const string MaskMobileInAdmin = "Sms.MaskMobileInAdmin";
        public const string AllowAdminViewFullMobile = "Sms.AllowAdminViewFullMobile";
        public const string AllowRetryFailed = "Sms.AllowRetryFailed";

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
