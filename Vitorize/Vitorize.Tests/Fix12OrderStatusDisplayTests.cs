using FluentAssertions;
using Vitorize.Shared.Enums;
using Xunit;
using AUI = Vitorize.Web.Helpers.AdminUiHelper;

namespace Vitorize.Tests;

/// <summary>
/// FIX-12 (Client Issue #14). The client-requested waiting/preparing terminology is a presentation
/// concern only: the persisted <see cref="OrderStatus"/> values and transitions are unchanged, and a
/// single helper feeds every Admin and Customer surface.
/// </summary>
public sealed class Fix12OrderStatusDisplayTests
{
    [Theory]
    [InlineData(OrderStatus.PendingPayment, "در انتظار پرداخت")]
    [InlineData(OrderStatus.Processing, "در حال آماده‌سازی")]
    [InlineData(OrderStatus.Completed, "تکمیل شده")]
    [InlineData(OrderStatus.Cancelled, "لغو شده")]
    [InlineData(OrderStatus.Failed, "ناموفق")]
    public void Order_status_display_uses_the_client_requested_terminology(OrderStatus status, string expected) =>
        AUI.OrderStatus((byte)status).Should().Be(expected);

    [Fact]
    public void Pending_payment_is_never_shown_as_a_generic_wait()
    {
        var label = AUI.OrderStatus((byte)OrderStatus.PendingPayment);

        label.Should().NotBe("در انتظار", "the customer must see what is actually being waited for");
        label.Should().Contain("پرداخت");
    }

    [Fact]
    public void Processing_is_no_longer_displayed_with_the_old_generic_processing_wording() =>
        AUI.OrderStatus((byte)OrderStatus.Processing).Should().NotBe("در حال پردازش");

    [Fact]
    public void Persisted_order_status_values_are_unchanged()
    {
        ((byte)OrderStatus.PendingPayment).Should().Be(1);
        ((byte)OrderStatus.Processing).Should().Be(2);
        ((byte)OrderStatus.Completed).Should().Be(3);
        ((byte)OrderStatus.Cancelled).Should().Be(4);
        ((byte)OrderStatus.Failed).Should().Be(5);
        ((byte)OrderStatus.Refunded).Should().Be(6);
        Enum.GetValues<OrderStatus>().Should().HaveCount(6, "FIX-12 introduces no new order states");
    }

    [Fact]
    public void Order_level_terminology_does_not_leak_into_item_level_delivery_or_payment_labels()
    {
        // Item-level truth must stay distinguishable from the order-level "being prepared" label.
        AUI.DeliveryStatus((byte)DeliveryStatus.Pending).Should().Be("در انتظار تحویل");
        AUI.DeliveryStatus((byte)DeliveryStatus.Delivered).Should().Be("تحویل شده");
        AUI.PaymentStatus((byte)PaymentStatus.Paid).Should().Be("پرداخت شده");
        AUI.PaymentStatus((byte)PaymentStatus.Pending).Should().Be("در انتظار پرداخت");
    }

    [Fact]
    public void Status_badge_intent_still_follows_the_existing_numeric_status()
    {
        AUI.StatusBadgeIntent((byte)OrderStatus.Completed, "order").Should().Be("success");
        AUI.StatusBadgeIntent((byte)OrderStatus.Processing, "order").Should().Be("info");
        AUI.StatusBadgeIntent((byte)OrderStatus.PendingPayment, "order").Should().Be("warning");
    }
}
