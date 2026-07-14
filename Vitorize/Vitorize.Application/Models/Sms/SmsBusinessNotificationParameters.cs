using Vitorize.Application.Common;

namespace Vitorize.Application.Models.Sms
{
    /// <summary>
    /// اعلان‌های تجاری فقط کد پیگیری عمومی دارند؛ هیچ عنوان، جزئیات یا داده حساسی وارد SMS نمی‌شود.
    /// </summary>
    public static class SmsBusinessNotificationParameters
    {
        public static IReadOnlyList<SmsTemplateParameter> OrderPaid(string orderNumber) =>
            Create(orderNumber);

        public static IReadOnlyList<SmsTemplateParameter> GiftCodeDelivered(string orderNumber) =>
            Create(orderNumber);

        public static IReadOnlyList<SmsTemplateParameter> TicketReply(string ticketNumber) =>
            Create(ticketNumber);

        public static IReadOnlyList<SmsTemplateParameter> WalletTopUp(string walletReference) =>
            Create(walletReference);

        public static IReadOnlyList<SmsTemplateParameter> VerificationApproved(string verificationReference) =>
            Create(verificationReference);

        public static IReadOnlyList<SmsTemplateParameter> VerificationRejected(string verificationReference) =>
            Create(verificationReference);

        public static IReadOnlyList<SmsTemplateParameter> Create(string orderNumber) =>
        [
            new(SmsTemplateParams.OrderNumber, orderNumber)
        ];
    }
}
