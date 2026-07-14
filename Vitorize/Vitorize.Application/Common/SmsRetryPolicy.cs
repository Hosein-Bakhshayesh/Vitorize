using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common
{
    public static class SmsRetryPolicy
    {
        public static bool IsRetryable(SmsFailureReason reason) => reason is
            SmsFailureReason.Network or
            SmsFailureReason.Timeout or
            SmsFailureReason.ProviderUnavailable or
            SmsFailureReason.TooManyRequests;
    }
}
