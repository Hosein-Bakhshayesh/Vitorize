namespace Vitorize.Shared.Enums
{
    public enum OrderStatus : byte
    {
        PendingPayment = 1,
        Processing = 2,
        Completed = 3,
        Cancelled = 4,
        Failed = 5,
        Refunded = 6
    }
}
