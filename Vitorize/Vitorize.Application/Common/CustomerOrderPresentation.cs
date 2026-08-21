using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// The minimum an order must expose to be described to its customer. Both the API's OrderDto and the
/// web project's own order model implement it, which is what lets one mapper serve every customer
/// surface without either layer duplicating the rules or referencing the other's types.
/// </summary>
public interface ICustomerOrderFacts
{
    byte Status { get; }
    byte PaymentStatus { get; }
    IEnumerable<ICustomerOrderItemFacts> ItemFacts { get; }
}

/// <summary>Per-line facts that affect what the customer is told. See <see cref="ICustomerOrderFacts"/>.</summary>
public interface ICustomerOrderItemFacts
{
    byte DeliveryType { get; }
    byte DeliveryStatus { get; }

    /// <summary>True when identity verification is what is holding this line up.</summary>
    bool KycBlocksFulfillment { get; }
}

/// <summary>
/// The customer-facing reading of one order, derived once and shared by every customer surface.
///
/// The governing rule is <b>payment state gates fulfillment presentation</b>: until money has
/// actually been captured, nothing may suggest that delivery is happening. Before this existed the
/// order list and the details page each mapped the raw enums themselves, so they could disagree with
/// each other and — because a freshly created item already carries DeliveryStatus.Pending — the
/// details page announced delivery progress on an order that had never been paid for.
/// </summary>
public sealed record CustomerOrderPresentation(
    CustomerOrderState State,
    string StatusLabel,
    string BadgeIntent,
    bool IsPaid,
    bool ShowFulfillmentProgress,
    string? FulfillmentNotice)
{
    /// <summary>True while the order is the customer's to pay, cancel or abandon.</summary>
    public bool IsAwaitingCustomerPayment =>
        State is CustomerOrderState.AwaitingPayment or CustomerOrderState.PaymentFailed;
}

public static class CustomerOrderPresenter
{
    /// <summary>
    /// Derives the customer-facing reading of an order. Deliberately takes the DTO both surfaces
    /// already load, so the list and the details page cannot drift apart.
    /// </summary>
    public static CustomerOrderPresentation Describe(ICustomerOrderFacts order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var items = order.ItemFacts as ICollection<ICustomerOrderItemFacts> ?? order.ItemFacts.ToList();
        var paid = order.PaymentStatus == (byte)PaymentStatus.Paid;

        // Terminal order states speak for themselves and never carry fulfillment language.
        switch (order.Status)
        {
            case (byte)OrderStatus.Cancelled:
                return new(CustomerOrderState.Cancelled, "لغو شده", "danger", paid, false, null);
            case (byte)OrderStatus.Refunded:
                return new(CustomerOrderState.Refunded, "بازگشت وجه", "muted", paid, false, null);
            case (byte)OrderStatus.Failed:
                return new(CustomerOrderState.Failed, "ناموفق", "danger", paid, false, null);
        }

        // Unpaid: one honest primary status, and an explicit statement that delivery has not begun.
        if (!paid)
        {
            var lastAttemptFailed = order.PaymentStatus is (byte)PaymentStatus.Failed or (byte)PaymentStatus.Cancelled;
            return lastAttemptFailed
                ? new(CustomerOrderState.PaymentFailed, "پرداخت ناموفق", "danger", false, false,
                      "پرداخت این سفارش کامل نشده است. تحویل پس از پرداخت آغاز می‌شود.")
                : new(CustomerOrderState.AwaitingPayment, "در انتظار پرداخت", "warning", false, false,
                      "تحویل پس از پرداخت آغاز می‌شود.");
        }

        // Paid. From here on fulfillment language is legitimate; pick the state that actually
        // describes what the order is waiting for, most blocking first.
        if (items.Any(x => x.KycBlocksFulfillment))
            return new(CustomerOrderState.AwaitingKyc, "در انتظار احراز هویت", "warning", true, true,
                       "برای تکمیل تحویل، احراز هویت لازم است.");

        if (items.Any(x => x.DeliveryType == (byte)DeliveryType.SupportRequired &&
                           x.DeliveryStatus != (byte)DeliveryStatus.Delivered))
            return new(CustomerOrderState.SupportInProgress, "در حال پیگیری پشتیبانی", "info", true, true,
                       "این سفارش توسط پشتیبانی پیگیری می‌شود.");

        if (items.Any(x => x.DeliveryStatus == (byte)DeliveryStatus.ManualReview))
            return new(CustomerOrderState.SupportInProgress, "در حال بررسی", "info", true, true,
                       "این سفارش در حال بررسی توسط کارشناسان است.");

        if (items.Count > 0 && items.All(x => x.DeliveryStatus == (byte)DeliveryStatus.Delivered))
            return new(CustomerOrderState.Delivered,
                       order.Status == (byte)OrderStatus.Completed ? "تکمیل شده" : "تحویل شده",
                       "success", true, true, null);

        if (order.Status == (byte)OrderStatus.Completed)
            return new(CustomerOrderState.Delivered, "تکمیل شده", "success", true, true, null);

        return new(CustomerOrderState.Delivering, "در حال آماده‌سازی", "info", true, true,
                   "سفارش شما پرداخت شده و در حال آماده‌سازی است.");
    }

    /// <summary>
    /// The per-item delivery wording. Returns null when the item needs no notice (its content is
    /// already shown). Never claims progress on an unpaid order.
    /// </summary>
    public static string? DescribeItemDelivery(CustomerOrderPresentation order, ICustomerOrderItemFacts item)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(item);

        if (item.DeliveryStatus == (byte)DeliveryStatus.Delivered)
            return null;

        if (!order.ShowFulfillmentProgress)
            return "تحویل پس از پرداخت آغاز می‌شود.";

        return item.DeliveryStatus switch
        {
            (byte)DeliveryStatus.ManualReview => "این آیتم در حال بررسی دستی است.",
            (byte)DeliveryStatus.Failed => "تحویل این آیتم ناموفق بود؛ پشتیبانی در حال بررسی است.",
            _ => "تحویل این آیتم در حال انجام است."
        };
    }

    /// <summary>
    /// The per-item delivery badge. On an unpaid order the raw «در انتظار تحویل» would still read as
    /// a queue the order is standing in, so it is stated as conditional on payment instead.
    /// </summary>
    public static string ItemDeliveryLabel(CustomerOrderPresentation order, ICustomerOrderItemFacts item)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(item);

        if (item.DeliveryStatus == (byte)DeliveryStatus.Delivered)
            return "تحویل شده";
        if (!order.ShowFulfillmentProgress)
            return "پس از پرداخت";

        return item.DeliveryStatus switch
        {
            (byte)DeliveryStatus.ManualReview => "بررسی دستی",
            (byte)DeliveryStatus.Failed => "ناموفق",
            _ => "در انتظار تحویل"
        };
    }
}
