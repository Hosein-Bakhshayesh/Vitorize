namespace Vitorize.Shared.Enums
{
    public enum SmsMessageStatus : byte
    {
        Pending = 0,
        Processing = 1,
        Sent = 2,
        Failed = 3,
        Retrying = 4,
        DeadLetter = 5,
        Disabled = 6,
        Cancelled = 7
    }

    public enum SmsSendType : byte
    {
        OtpTemplate = 1,
        NotificationTemplate = 2,
        CustomText = 3
    }
}
