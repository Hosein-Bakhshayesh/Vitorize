using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests.Unit;

public sealed class OrderItemFulfillmentEligibilityTests
{
    [Fact]
    public void Absent_lifecycle_preserves_legacy_fulfillment_behavior() =>
        Assert.True(OrderItemFulfillmentEligibility.CanFulfill(new OrderItem()));

    [Theory]
    [InlineData(OrderItemKycStatus.NotRequired, true)]
    [InlineData(OrderItemKycStatus.Satisfied, true)]
    [InlineData(OrderItemKycStatus.AwaitingSubmission, false)]
    [InlineData(OrderItemKycStatus.AwaitingReview, false)]
    [InlineData(OrderItemKycStatus.Rejected, false)]
    [InlineData(OrderItemKycStatus.FinalRejected, false)]
    [InlineData(OrderItemKycStatus.Expired, false)]
    public void Lifecycle_status_is_the_only_managed_fulfillment_decision(OrderItemKycStatus status, bool expected)
    {
        var item = new OrderItem { KycLifecycleState = new OrderItemKycState { Status = (byte)status } };
        Assert.Equal(expected, OrderItemFulfillmentEligibility.CanFulfill(item));
    }
}
