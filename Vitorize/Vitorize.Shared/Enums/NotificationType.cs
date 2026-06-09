namespace Vitorize.Shared.Enums
{
    public enum NotificationType : byte
    {
        OrderCreated = 1,
        PaymentSucceeded = 2,
        GiftCodeDelivered = 3,
        VerificationApproved = 4,
        VerificationRejected = 5,
        TicketCreated = 6,
        TicketReply = 7,
        WalletCharged = 8
    }
}