namespace Vitorize.Application.Common
{
    /// <summary>
    /// کلیدهای تنظیمات پیامک در جدول Settings (گروه «SMS»).
    /// این کلیدها هرگز نباید از طریق endpoint عمومی تنظیمات برگردانده شوند.
    /// </summary>
    public static class SmsSettingKeys
    {
        public const string Group = "SMS";

        // Asanak is the only supported transport. Its connection settings are kept separate
        // so no generic or legacy provider credentials can accidentally be used for delivery.
        public const string AsanakUsername = "Sms.AsanakUsername";
        public const string AsanakPassword = "Sms.AsanakPassword";
        public const string AsanakSource = "Sms.AsanakSource";

        // Only OTP has an active template. Notification template keys below are retained
        // solely so V0036 can remove values from previously deployed databases.
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

        /// <summary>کلیدهای تاریخی قالب اعلان که با V0036 حذف می‌شوند.</summary>
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
        public const string MaxCustomRecipients = "Sms.MaxCustomRecipients";
        public const string MaxCustomTextLength = "Sms.MaxCustomTextLength";
        public const string HistoryRetentionDays = "Sms.HistoryRetentionDays";
        public const string MaskMobileInAdmin = "Sms.MaskMobileInAdmin";
        public const string AllowAdminViewFullMobile = "Sms.AllowAdminViewFullMobile";

        /// <summary>
        /// کلیدهایی که در نسخه‌های پیشین امکان خاموش‌کردن پیامک یا مسیر ارسال آن را می‌دادند.
        /// پیامک همیشه فعال است و این مقادیر با مهاجرت حذف می‌شوند.
        /// </summary>
        public static readonly IReadOnlySet<string> DeprecatedKeys = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "SmsEnabled",
            "Sms.IsEnabled",
            "Sms.CustomSendEnabled",
            "Sms.CustomTextEnabled",
            "Sms.UseOutbox",
            "Sms.RequireConfirmation",
            "Sms.AllowImmediateSend",
            "Sms.AllowRetryFailed",
            // V0035 deletes these settings. Keep them hidden defensively until that
            // migration has run, so an older database cannot expose obsolete inputs.
            "Sms.Provider",
            "Sms.ApiKey",
            "Sms.DefaultLineNumber",
            "Sms.SenderName",
            "Sms.NotificationTemplateId",
            "Sms.OrderPaidTemplateId",
            "Sms.OrderCompletedTemplateId",
            "Sms.OrderStatusChangedTemplateId",
            "Sms.GiftCodeDeliveredTemplateId",
            "Sms.TicketReplyTemplateId",
            "Sms.VerificationApprovedTemplateId",
            "Sms.VerificationRejectedTemplateId",
            "Sms.WalletTopUpSuccessTemplateId"
        };

        /// <summary>
        /// کلیدهای محرمانه یا داخلی که نباید در پاسخ عمومی/کلاینت آشکار شوند.
        /// </summary>
        public static readonly IReadOnlySet<string> SecretKeys =
            new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                AsanakUsername,
                AsanakPassword,
                AsanakSource
            };
    }

}
