namespace Vitorize.Shared.Enums
{
    public enum OutboxMessageStatus : byte
    {
        Pending = 0,
        Processing = 1,
        Processed = 2,
        Failed = 3
    }
}