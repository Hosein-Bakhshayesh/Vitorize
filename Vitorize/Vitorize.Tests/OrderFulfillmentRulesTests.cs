using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class OrderFulfillmentRulesTests
{
    [Theory]
    [InlineData((byte)PaymentStatus.Pending, false)]
    [InlineData((byte)PaymentStatus.Paid, true)]
    [InlineData((byte)PaymentStatus.Failed, false)]
    public void Is_paid_recognizes_only_paid_orders(byte status, bool expected) =>
        Assert.Equal(expected, OrderFulfillmentRules.IsPaid(status));

    [Fact]
    public void Completion_requires_paid_and_every_order_item_delivered()
    {
        var delivered = (byte)DeliveryStatus.Delivered;
        var pending = (byte)DeliveryStatus.Pending;

        Assert.True(OrderFulfillmentRules.CanComplete((byte)PaymentStatus.Paid, [delivered, delivered]));
        Assert.False(OrderFulfillmentRules.CanComplete((byte)PaymentStatus.Pending, [delivered, delivered]));
        Assert.False(OrderFulfillmentRules.CanComplete((byte)PaymentStatus.Paid, [delivered, pending]));
        Assert.Equal(1, OrderFulfillmentRules.OutstandingCount([delivered, pending]));
    }
}
