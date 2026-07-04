namespace Vitorize.Shared.Enums
{
    public enum WalletReferenceType : byte
    {
        ManualAdminCharge = 1,
        ManualAdminWithdraw = 2,
        OrderPayment = 3,
        Refund = 4,
        Cashback = 5,
        WalletTopUp = 6
    }
}