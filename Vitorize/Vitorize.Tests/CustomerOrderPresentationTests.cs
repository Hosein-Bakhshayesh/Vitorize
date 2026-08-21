using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The customer-facing reading of an order. The governing rule under test is that payment state gates
/// fulfillment presentation: before a successful payment nothing may suggest delivery is happening.
/// The reported defect was an order badged «در انتظار پرداخت» whose items simultaneously announced
/// «تحویل سفارش در حال انجام است», because item delivery wording was derived from DeliveryStatus
/// alone and a freshly created item is already Pending.
/// </summary>
public sealed class CustomerOrderPresentationTests
{
    private static OrderDto Order(
        OrderStatus status,
        PaymentStatus payment,
        params (DeliveryType Type, DeliveryStatus Status, bool KycBlocks)[] items) =>
        new()
        {
            Status = (byte)status,
            PaymentStatus = (byte)payment,
            Items = items.Select(x => new OrderItemDto
            {
                Id = Guid.NewGuid(),
                DeliveryType = (byte)x.Type,
                DeliveryStatus = (byte)x.Status,
                Kyc = x.KycBlocks
                    ? new OrderItemKycProjectionDto { BlocksFulfillment = true }
                    : null
            }).ToList()
        };

    private static OrderDto UnpaidManualOrder() =>
        Order(OrderStatus.PendingPayment, PaymentStatus.Pending,
              (DeliveryType.Manual, DeliveryStatus.Pending, false));

    // ---------------------------------------------------------------- unpaid

    [Fact]
    public void An_unpaid_order_reads_as_awaiting_payment()
    {
        var view = CustomerOrderPresenter.Describe(UnpaidManualOrder());

        Assert.Equal(CustomerOrderState.AwaitingPayment, view.State);
        Assert.Equal("در انتظار پرداخت", view.StatusLabel);
        Assert.False(view.IsPaid);
    }

    [Fact]
    public void An_unpaid_order_never_presents_fulfillment_progress()
    {
        var view = CustomerOrderPresenter.Describe(UnpaidManualOrder());

        Assert.False(view.ShowFulfillmentProgress);
        Assert.Equal("تحویل پس از پرداخت آغاز می‌شود.", view.FulfillmentNotice);
    }

    [Fact]
    public void The_reported_contradiction_cannot_be_rendered()
    {
        // The exact defect: an unpaid order whose item wording claimed delivery was under way.
        var order = UnpaidManualOrder();
        var view = CustomerOrderPresenter.Describe(order);
        var item = order.Items[0];

        var notice = CustomerOrderPresenter.DescribeItemDelivery(view, item);
        var badge = CustomerOrderPresenter.ItemDeliveryLabel(view, item);

        Assert.Equal("در انتظار پرداخت", view.StatusLabel);
        Assert.DoesNotContain("در حال انجام", notice);
        Assert.Equal("تحویل پس از پرداخت آغاز می‌شود.", notice);
        Assert.Equal("پس از پرداخت", badge);
    }

    [Fact]
    public void A_failed_payment_reads_as_payment_failed_and_still_hides_delivery()
    {
        var view = CustomerOrderPresenter.Describe(
            Order(OrderStatus.PendingPayment, PaymentStatus.Failed,
                  (DeliveryType.Manual, DeliveryStatus.Pending, false)));

        Assert.Equal(CustomerOrderState.PaymentFailed, view.State);
        Assert.False(view.ShowFulfillmentProgress);
        Assert.True(view.IsAwaitingCustomerPayment);
    }

    // ---------------------------------------------------------------- paid

    [Fact]
    public void A_paid_order_being_prepared_reads_as_processing_and_may_show_progress()
    {
        var order = Order(OrderStatus.Processing, PaymentStatus.Paid,
                          (DeliveryType.Manual, DeliveryStatus.Pending, false));
        var view = CustomerOrderPresenter.Describe(order);

        Assert.Equal(CustomerOrderState.Delivering, view.State);
        Assert.True(view.ShowFulfillmentProgress);
        Assert.Equal("تحویل این آیتم در حال انجام است.",
            CustomerOrderPresenter.DescribeItemDelivery(view, order.Items[0]));
    }

    [Fact]
    public void Blocking_kyc_outranks_generic_progress()
    {
        var view = CustomerOrderPresenter.Describe(
            Order(OrderStatus.Processing, PaymentStatus.Paid,
                  (DeliveryType.Manual, DeliveryStatus.Pending, true)));

        Assert.Equal(CustomerOrderState.AwaitingKyc, view.State);
        Assert.True(view.ShowFulfillmentProgress);
    }

    [Fact]
    public void A_support_required_line_reads_as_support_in_progress()
    {
        var view = CustomerOrderPresenter.Describe(
            Order(OrderStatus.Processing, PaymentStatus.Paid,
                  (DeliveryType.SupportRequired, DeliveryStatus.Pending, false)));

        Assert.Equal(CustomerOrderState.SupportInProgress, view.State);
    }

    [Fact]
    public void Fully_delivered_items_read_as_delivered_and_carry_no_notice()
    {
        var order = Order(OrderStatus.Completed, PaymentStatus.Paid,
                          (DeliveryType.Instant, DeliveryStatus.Delivered, false));
        var view = CustomerOrderPresenter.Describe(order);

        Assert.Equal(CustomerOrderState.Delivered, view.State);
        Assert.Null(view.FulfillmentNotice);
        Assert.Null(CustomerOrderPresenter.DescribeItemDelivery(view, order.Items[0]));
        Assert.Equal("تحویل شده", CustomerOrderPresenter.ItemDeliveryLabel(view, order.Items[0]));
    }

    // ---------------------------------------------------------------- terminal

    [Fact]
    public void A_cancelled_order_reads_as_cancelled_with_no_fulfillment_language()
    {
        var order = Order(OrderStatus.Cancelled, PaymentStatus.Pending,
                          (DeliveryType.Manual, DeliveryStatus.Pending, false));
        var view = CustomerOrderPresenter.Describe(order);

        Assert.Equal(CustomerOrderState.Cancelled, view.State);
        Assert.Equal("لغو شده", view.StatusLabel);
        Assert.False(view.ShowFulfillmentProgress);
        Assert.Null(view.FulfillmentNotice);
        Assert.False(view.IsAwaitingCustomerPayment);
    }

    [Fact]
    public void A_refunded_order_is_not_described_as_awaiting_payment()
    {
        var view = CustomerOrderPresenter.Describe(
            Order(OrderStatus.Refunded, PaymentStatus.Refunded,
                  (DeliveryType.Instant, DeliveryStatus.Delivered, false)));

        Assert.Equal(CustomerOrderState.Refunded, view.State);
        Assert.False(view.IsAwaitingCustomerPayment);
    }

    [Fact]
    public void An_order_with_no_items_does_not_claim_delivery()
    {
        // Defensive: an empty projection must not fall through to "delivered".
        var view = CustomerOrderPresenter.Describe(Order(OrderStatus.Processing, PaymentStatus.Paid));

        Assert.Equal(CustomerOrderState.Delivering, view.State);
    }

    [Fact]
    public void The_list_and_the_details_page_read_the_same_order_identically()
    {
        // Both surfaces call the same mapper over the same facts, so agreement is structural. This
        // pins it: a summary projection (no deliveries or input values loaded) and a details
        // projection of the same order must produce the same state.
        var summary = UnpaidManualOrder();
        var details = UnpaidManualOrder();
        details.Items[0].Deliveries.Clear();

        Assert.Equal(
            CustomerOrderPresenter.Describe(summary).State,
            CustomerOrderPresenter.Describe(details).State);
    }
}
