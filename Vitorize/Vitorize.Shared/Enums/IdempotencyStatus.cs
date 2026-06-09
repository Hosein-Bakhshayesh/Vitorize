namespace Vitorize.Shared.Enums
{
    public enum IdempotencyStatus : byte
    {
        Processing = 1,
        Completed = 2,
        Failed = 3
    }
}