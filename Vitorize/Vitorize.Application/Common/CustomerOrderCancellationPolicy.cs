using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// The single authoritative rule for whether the owning customer may cancel their own order, and
/// whether a cancelled order may then be hidden from their panel.
///
/// The hard constraint is that there is no provider-side void operation: once a gateway session
/// exists, the customer can still complete it at the bank and Zarinpal will report a success that
/// arrives after the cancellation. Cancelling underneath a live session would therefore create an
/// order that is simultaneously cancelled and paid. So cancellation is only offered while nothing
/// can still settle, and <see cref="PaymentService"/> independently refuses to fulfil a cancelled
/// order if a late success ever does arrive — the rule here prevents the race, that check contains
/// it.
///
/// Mirrors <see cref="PaymentAttemptPolicy"/>: pure, operates only on the persisted snapshot, and
/// returns null when the operation is allowed.
/// </summary>
public static class CustomerOrderCancellationPolicy
{
    /// <summary>Gateways whose attempts are settled by the customer at the provider, not by us.</summary>
    private static bool IsProviderSettled(Payment payment) =>
        !string.Equals(payment.Gateway, "Wallet", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Null when the owning customer may cancel; otherwise the Persian reason to show them.
    /// </summary>
    /// <param name="gatewayAttemptLifetimeMinutes">
    /// How long a gateway attempt stays live, from the same payment timing options the payment
    /// service uses to expire attempts. An attempt older than this can no longer be completed.
    /// </param>
    public static string? GetCancelBlockReason(
        Order order,
        IEnumerable<Payment> payments,
        IEnumerable<OrderItem> items,
        DateTime utcNow,
        int gatewayAttemptLifetimeMinutes)
    {
        ArgumentNullException.ThrowIfNull(order);
        var attempts = payments as ICollection<Payment> ?? payments?.ToList() ?? [];
        var lines = items as ICollection<OrderItem> ?? items?.ToList() ?? [];

        if (order.Status == (byte)OrderStatus.Cancelled)
            return "این سفارش قبلاً لغو شده است.";

        // Money captured — by gateway or from the wallet — is never "simply unpaid". A paid order
        // leaves the customer's hands entirely and can only be unwound by the refund workflow.
        if (order.PaymentStatus == (byte)PaymentStatus.Paid ||
            attempts.Any(x => x.Status == (byte)PaymentStatus.Paid))
            return "سفارش پرداخت‌شده قابل لغو نیست. برای بازگشت وجه با پشتیبانی تماس بگیرید.";

        if (order.PaymentStatus == (byte)PaymentStatus.Refunded)
            return "این سفارش بازپرداخت شده است.";

        // Anything past PendingPayment has already been acted on by the shop.
        if (order.Status != (byte)OrderStatus.PendingPayment)
            return "این سفارش در وضعیتی نیست که قابل لغو باشد.";

        // Defence in depth: a wallet attempt that is not terminally closed may still be mid-debit.
        if (attempts.Any(x => !IsProviderSettled(x) &&
                              x.Status is not ((byte)PaymentStatus.Failed or (byte)PaymentStatus.Cancelled)))
            return "پرداخت از کیف پول برای این سفارش در جریان است. لطفاً چند لحظه دیگر تلاش کنید.";

        // Real fulfillment must never be undone by a customer click, whatever the payment says.
        if (lines.Any(x => x.DeliveryStatus != (byte)DeliveryStatus.Pending) ||
            lines.Any(x => x.OrderItemDeliveries.Count > 0))
            return "برای این سفارش فرآیند تحویل آغاز شده است. لطفاً با پشتیبانی تماس بگیرید.";

        // A live provider session can still be completed at the bank after this click.
        var lifetime = TimeSpan.FromMinutes(Math.Max(1, gatewayAttemptLifetimeMinutes));
        var live = attempts.Any(x =>
            x.Status == (byte)PaymentStatus.Pending &&
            IsProviderSettled(x) &&
            x.RequestedAt > utcNow.Subtract(lifetime) &&
            (!string.IsNullOrWhiteSpace(x.Authority) ||
             string.Equals(x.ProviderStatusCode, "INITIALIZING", StringComparison.Ordinal) ||
             string.Equals(x.ProviderStatusCode, "VERIFYING", StringComparison.Ordinal) ||
             string.Equals(x.ProviderStatusCode, "VERIFYING_LATE", StringComparison.Ordinal)));

        if (live)
            return "یک پرداخت باز برای این سفارش در جریان است. اگر پرداخت را انجام نداده‌اید، "
                 + "پس از پایان اعتبار آن (حدود "
                 + Math.Max(1, gatewayAttemptLifetimeMinutes)
                 + " دقیقه) امکان لغو فعال می‌شود.";

        return null;
    }

    /// <summary>
    /// Null when the customer may hide the order from their own panel. Hiding is presentation only:
    /// the order, its payment attempts and its history stay intact and fully visible to Admin.
    /// </summary>
    public static string? GetHideBlockReason(Order order, IEnumerable<Payment> payments)
    {
        ArgumentNullException.ThrowIfNull(order);
        var attempts = payments as ICollection<Payment> ?? payments?.ToList() ?? [];

        if (order.PaymentStatus == (byte)PaymentStatus.Paid ||
            attempts.Any(x => x.Status == (byte)PaymentStatus.Paid))
            return "سفارش پرداخت‌شده از فهرست شما حذف نمی‌شود.";

        // Only a settled-and-closed unpaid order may leave the customer's list. An order still
        // awaiting payment stays visible so it cannot be forgotten while it is still payable.
        if (order.Status is not ((byte)OrderStatus.Cancelled or (byte)OrderStatus.Failed))
            return "تنها سفارش‌های لغو یا ناموفق را می‌توان از فهرست حذف کرد.";

        return null;
    }
}
