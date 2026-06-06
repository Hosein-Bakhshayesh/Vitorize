namespace Vitorize.Domain.Enums
{
    public enum DeliveryStatus : byte
    {
        Pending = 1,
        Delivered = 2,
        ManualReview = 3,
        Failed = 4
    }
}