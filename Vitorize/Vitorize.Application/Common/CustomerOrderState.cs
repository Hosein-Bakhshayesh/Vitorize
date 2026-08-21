namespace Vitorize.Application.Common;

/// <summary>
/// What an order means to the customer who placed it.
///
/// The persisted model spreads an order's condition across three independent values — the order
/// status, the payment status and a per-item delivery status — and each customer surface used to
/// interpret them on its own. That is how a single page could show «در انتظار پرداخت» next to
/// «تحویل سفارش در حال انجام است»: the order was unpaid while its items were, technically, in the
/// Pending delivery state that predates any payment.
///
/// This enum is the one customer-facing vocabulary those three values collapse into. It is a
/// presentation concept only; nothing is persisted with these names and no domain state was
/// invented to fit them.
/// </summary>
public enum CustomerOrderState : byte
{
    /// <summary>Not paid yet and still payable.</summary>
    AwaitingPayment = 1,

    /// <summary>The last attempt did not settle. Still the customer's move.</summary>
    PaymentFailed = 2,

    /// <summary>Paid; the shop is preparing the items.</summary>
    Processing = 3,

    /// <summary>Paid, but at least one item is waiting on the customer's identity documents.</summary>
    AwaitingKyc = 4,

    /// <summary>Paid; at least one item is being handled by support rather than delivered automatically.</summary>
    SupportInProgress = 5,

    /// <summary>Paid and delivery genuinely under way.</summary>
    Delivering = 6,

    /// <summary>Everything owed to the customer has been delivered.</summary>
    Delivered = 7,

    /// <summary>Cancelled before payment — by the customer or by an administrator.</summary>
    Cancelled = 8,

    /// <summary>Closed unsuccessfully by the shop.</summary>
    Failed = 9,

    /// <summary>Paid and then refunded.</summary>
    Refunded = 10
}
