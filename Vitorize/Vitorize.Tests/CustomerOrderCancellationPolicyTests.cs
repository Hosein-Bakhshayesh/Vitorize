using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// When the owning customer may cancel their own order, and when they may hide it afterwards.
///
/// The constraint that shapes every case here: there is no provider-side void, so a gateway session
/// that is still live can settle at the bank after the click. Cancelling underneath one would produce
/// an order that is both cancelled and paid, so cancellation is refused while anything can still
/// settle.
/// </summary>
public sealed class CustomerOrderCancellationPolicyTests
{
    private const int AttemptLifetimeMinutes = 30;
    private static readonly DateTime Now = new(2026, 8, 21, 12, 0, 0, DateTimeKind.Utc);

    private static Order Order(OrderStatus status = OrderStatus.PendingPayment,
                               PaymentStatus payment = PaymentStatus.Pending) =>
        new() { Id = Guid.NewGuid(), OrderNumber = "VZ-1", Status = (byte)status, PaymentStatus = (byte)payment, FinalAmount = 10_000m };

    private static Payment Attempt(PaymentStatus status, string gateway = "Zarinpal",
                                   string? authority = null, string? providerStatus = null,
                                   int ageMinutes = 1) =>
        new()
        {
            Id = Guid.NewGuid(), Status = (byte)status, Gateway = gateway, Authority = authority,
            ProviderStatusCode = providerStatus, RequestedAt = Now.AddMinutes(-ageMinutes), Amount = 10_000m
        };

    private static OrderItem Item(DeliveryStatus delivery = DeliveryStatus.Pending, bool delivered = false)
    {
        var item = new OrderItem { Id = Guid.NewGuid(), DeliveryStatus = (byte)delivery };
        if (delivered)
            item.OrderItemDeliveries.Add(new OrderItemDelivery { Id = Guid.NewGuid() });
        return item;
    }

    private static string? Cancel(Order order, IEnumerable<Payment>? payments = null, IEnumerable<OrderItem>? items = null) =>
        CustomerOrderCancellationPolicy.GetCancelBlockReason(
            order, payments ?? [], items ?? [Item()], Now, AttemptLifetimeMinutes);

    // ---------------------------------------------------------------- allowed

    [Fact]
    public void A_never_paid_order_with_no_payment_row_is_cancellable()
    {
        Assert.Null(Cancel(Order()));
    }

    [Fact]
    public void A_failed_attempt_does_not_block_cancellation()
    {
        Assert.Null(Cancel(Order(), [Attempt(PaymentStatus.Failed, authority: "A0001")]));
    }

    [Fact]
    public void An_attempt_the_customer_abandoned_at_the_gateway_blocks_only_until_it_expires()
    {
        var live = Attempt(PaymentStatus.Pending, authority: "A0001", ageMinutes: 5);
        Assert.NotNull(Cancel(Order(), [live]));

        // Past its lifetime the session can no longer be completed, so the order becomes cancellable.
        var expired = Attempt(PaymentStatus.Pending, authority: "A0001", ageMinutes: AttemptLifetimeMinutes + 1);
        Assert.Null(Cancel(Order(), [expired]));
    }

    [Fact]
    public void An_attempt_that_never_reached_the_provider_does_not_block()
    {
        // No authority and not mid-initialisation: nothing exists at the provider to settle.
        Assert.Null(Cancel(Order(), [Attempt(PaymentStatus.Pending, authority: null)]));
    }

    // ---------------------------------------------------------------- refused

    [Fact]
    public void A_paid_order_is_never_cancellable()
    {
        Assert.NotNull(Cancel(Order(OrderStatus.Processing, PaymentStatus.Paid)));
        // Even if the order header has not caught up, a paid attempt is decisive.
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Paid)]));
    }

    [Fact]
    public void A_wallet_debited_order_is_never_treated_as_simply_unpaid()
    {
        // A wallet attempt that is not terminally closed may still be mid-debit.
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Pending, gateway: "Wallet")]));
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Paid, gateway: "Wallet")]));
    }

    [Fact]
    public void An_order_being_initialised_for_payment_right_now_is_not_cancellable()
    {
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Pending, providerStatus: "INITIALIZING")]));
    }

    [Fact]
    public void An_order_mid_verification_is_not_cancellable()
    {
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Pending, providerStatus: "VERIFYING")]));
        Assert.NotNull(Cancel(Order(), [Attempt(PaymentStatus.Pending, providerStatus: "VERIFYING_LATE")]));
    }

    [Fact]
    public void An_order_that_entered_fulfillment_is_not_cancellable()
    {
        Assert.NotNull(Cancel(Order(), items: [Item(DeliveryStatus.Delivered)]));
        Assert.NotNull(Cancel(Order(), items: [Item(DeliveryStatus.ManualReview)]));
        // A delivery record is decisive even when the status byte still reads Pending.
        Assert.NotNull(Cancel(Order(), items: [Item(delivered: true)]));
    }

    [Fact]
    public void A_delivered_or_completed_order_is_not_cancellable()
    {
        Assert.NotNull(Cancel(Order(OrderStatus.Completed, PaymentStatus.Paid)));
    }

    [Fact]
    public void Cancelling_twice_is_refused_with_a_clear_reason_rather_than_repeating_the_write()
    {
        var reason = Cancel(Order(OrderStatus.Cancelled));

        Assert.NotNull(reason);
        Assert.Contains("قبلاً لغو", reason);
    }

    [Fact]
    public void A_refunded_order_is_not_cancellable()
    {
        Assert.NotNull(Cancel(Order(OrderStatus.Refunded, PaymentStatus.Refunded)));
    }

    // ---------------------------------------------------------------- hiding

    [Fact]
    public void Only_a_settled_unpaid_order_may_be_hidden()
    {
        Assert.Null(CustomerOrderCancellationPolicy.GetHideBlockReason(Order(OrderStatus.Cancelled), []));
        Assert.Null(CustomerOrderCancellationPolicy.GetHideBlockReason(Order(OrderStatus.Failed), []));
    }

    [Fact]
    public void An_order_still_awaiting_payment_may_not_be_hidden()
    {
        // Hiding a payable order would let a customer lose track of something still chargeable.
        Assert.NotNull(CustomerOrderCancellationPolicy.GetHideBlockReason(Order(), []));
    }

    [Fact]
    public void A_paid_order_may_never_be_hidden()
    {
        Assert.NotNull(CustomerOrderCancellationPolicy.GetHideBlockReason(
            Order(OrderStatus.Completed, PaymentStatus.Paid), []));
        // Nor when only the attempt records the payment.
        Assert.NotNull(CustomerOrderCancellationPolicy.GetHideBlockReason(
            Order(OrderStatus.Cancelled), [Attempt(PaymentStatus.Paid)]));
    }
}
