using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// The single authoritative rule for starting or retrying an external payment for an order.
/// It deliberately operates only on the persisted order snapshot and payment history; cart and
/// current catalogue values must never influence a retry.
/// </summary>
public static class PaymentAttemptPolicy
{
    public static string? GetIneligibilityReason(Order order, IEnumerable<Payment> payments)
    {
        if (order.Status != (byte)OrderStatus.PendingPayment)
            return "این سفارش دیگر در انتظار پرداخت نیست.";

        if (order.PaymentStatus == (byte)PaymentStatus.Paid ||
            payments.Any(x => x.Status == (byte)PaymentStatus.Paid))
            return "این سفارش قبلاً پرداخت شده است.";

        if (order.FinalAmount <= 0)
            return "مبلغ سفارش معتبر نیست.";

        return null;
    }
}
