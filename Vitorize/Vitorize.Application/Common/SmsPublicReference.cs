using System.Security.Cryptography;
using System.Text;

namespace Vitorize.Application.Common
{
    /// <summary>
    /// یک کد پیگیری عمومی و پایدار می‌سازد بدون آن‌که GUID یا شناسه داخلی را در SMS آشکار کند.
    /// </summary>
    public static class SmsPublicReference
    {
        public static string ForTicket(Guid ticketId) => Create("TK", ticketId.ToString("N"));

        public static string ForWallet(Guid transactionId) => Create("WL", transactionId.ToString("N"));

        public static string ForWalletTopUp(Guid topUpId, string? safeProviderReference = null) =>
            Create("WL", string.IsNullOrWhiteSpace(safeProviderReference)
                ? topUpId.ToString("N")
                : safeProviderReference.Trim());

        public static string ForVerification(Guid verificationId) =>
            Create("VF", verificationId.ToString("N"));

        internal static string FromLegacyValue(string prefix, string value) =>
            Create(prefix, value);

        private static string Create(string prefix, string source)
        {
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
            return $"{prefix}-{Convert.ToHexString(hash.AsSpan(0, 6))}";
        }
    }
}
