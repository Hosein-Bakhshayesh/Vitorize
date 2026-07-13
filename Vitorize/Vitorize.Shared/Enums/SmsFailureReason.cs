namespace Vitorize.Shared.Enums
{
    /// <summary>
    /// دلیل شکست ارسال پیامک؛ خطاهای خام ارائه‌دهنده به این نوع داخلی نگاشت می‌شوند
    /// تا هرگز به کاربر نهایی نشت نکنند.
    /// </summary>
    public enum SmsFailureReason : byte
    {
        None = 0,
        Disabled = 1,
        NotConfigured = 2,
        InvalidMobile = 3,
        InvalidTemplate = 4,
        InvalidParameter = 5,
        InvalidLineNumber = 6,
        InsufficientCredit = 7,
        Unauthorized = 8,
        AccessDenied = 9,
        TooManyRequests = 10,
        Timeout = 11,
        Network = 12,
        ProviderUnavailable = 13,
        Unknown = 14
    }
}
