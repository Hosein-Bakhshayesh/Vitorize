using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Common
{
    /// <summary>
    /// قرارداد دو قالب تاییدشده SMS.ir. نام پارامترها به حروف بزرگ/کوچک حساس است.
    /// </summary>
    public static class SmsTemplateContract
    {
        public static readonly IReadOnlyList<string> OtpParameterNames =
            [SmsTemplateParams.Code, SmsTemplateParams.Expire];

        public static readonly IReadOnlyList<string> NotificationParameterNames =
            [SmsTemplateParams.OrderNumber];

        public static IReadOnlyList<string>? GetRequiredParameterNames(string templateKey)
        {
            if (SmsTemplateKeys.IsOtp(templateKey))
                return OtpParameterNames;

            if (SmsTemplateKeys.IsNotification(templateKey))
                return NotificationParameterNames;

            return null;
        }

        public static bool HasExactParameters(
            string templateKey,
            IReadOnlyList<SmsTemplateParameter>? parameters)
        {
            var required = GetRequiredParameterNames(templateKey);
            if (required is null || parameters is null || parameters.Count != required.Count)
                return false;

            if (parameters.Any(x =>
                    string.IsNullOrWhiteSpace(x.Name) ||
                    string.IsNullOrWhiteSpace(x.Value)))
                return false;

            var actual = parameters.Select(x => x.Name).ToArray();
            return actual.Distinct(StringComparer.Ordinal).Count() == required.Count &&
                   required.All(name => actual.Contains(name, StringComparer.Ordinal));
        }

        /// <summary>
        /// فقط برای پیام‌های از قبل ذخیره‌شده در Outbox: ORDER_NUMBER را استخراج
        /// یا REFERENCE قدیمی را به آن تبدیل می‌کند و TITLE/DETAIL قدیمی را کنار
        /// می‌گذارد. مسیر ارسال عادی همه نام‌های قدیمی را رد می‌کند.
        /// </summary>
        public static IReadOnlyList<SmsTemplateParameter> NormalizeQueuedParameters(
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters)
        {
            if (!SmsTemplateKeys.IsNotification(templateKey))
                return parameters;

            const string legacyTitle = "TITLE";
            const string legacyReference = "REFERENCE";
            const string legacyDetail = "DETAIL";

            var normalized = new List<SmsTemplateParameter>();
            var orderNumbers = parameters
                .Where(x => x.Name == SmsTemplateParams.OrderNumber)
                .ToList();
            var legacyReferences = parameters
                .Where(x => x.Name == legacyReference)
                .ToList();

            if (orderNumbers.Count > 0)
            {
                normalized.AddRange(orderNumbers);
            }
            else if (legacyReferences.Count > 0)
            {
                normalized.AddRange(legacyReferences.Select(x => new SmsTemplateParameter(
                    SmsTemplateParams.OrderNumber,
                    NormalizeLegacyReference(templateKey, x.Value))));
            }

            normalized.AddRange(parameters.Where(x =>
                x.Name != legacyTitle &&
                x.Name != SmsTemplateParams.OrderNumber &&
                x.Name != legacyReference &&
                x.Name != legacyDetail));

            return normalized;
        }

        private static string NormalizeLegacyReference(string templateKey, string value)
        {
            var trimmed = value?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
                return string.Empty;

            if (templateKey is SmsTemplateKeys.OrderCreated or
                SmsTemplateKeys.OrderPaid or
                SmsTemplateKeys.OrderCompleted or
                SmsTemplateKeys.OrderStatusChanged or
                SmsTemplateKeys.OrderCancelled or
                SmsTemplateKeys.GiftCodeDelivered or
                SmsTemplateKeys.UniversalNotification)
            {
                const string orderPrefix = "سفارش ";
                return trimmed.StartsWith(orderPrefix, StringComparison.Ordinal)
                    ? trimmed[orderPrefix.Length..].Trim()
                    : trimmed;
            }

            if (templateKey is SmsTemplateKeys.TicketReply or SmsTemplateKeys.TicketClosed or SmsTemplateKeys.TicketReopened)
                return SmsPublicReference.FromLegacyValue("TK", trimmed);

            if (templateKey is SmsTemplateKeys.WalletTopUpSuccess or SmsTemplateKeys.WalletTransaction)
                return SmsPublicReference.FromLegacyValue("WL", trimmed);

            if (templateKey is SmsTemplateKeys.VerificationApproved or SmsTemplateKeys.VerificationRejected)
                return SmsPublicReference.FromLegacyValue("VF", trimmed);

            return trimmed;
        }
    }
}
