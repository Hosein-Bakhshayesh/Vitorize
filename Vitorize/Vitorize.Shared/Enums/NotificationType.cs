namespace Vitorize.Shared.Enums
{
    public enum NotificationType : byte
    {
        OrderCreated = 1,
        OrderPaid = 2,
        OrderCompleted = 3,
        OrderCancelled = 4,

        PaymentSucceeded = 10,
        PaymentFailed = 11,

        GiftCodeDelivered = 20,

        WalletCharged = 30,
        WalletDebited = 31,
        WalletRefunded = 32,

        VerificationSubmitted = 40,
        VerificationApproved = 41,
        VerificationRejected = 42,

        TicketCreated = 50,
        TicketReply = 51,
        TicketClosed = 52,

        SystemMessage = 90
    }
}