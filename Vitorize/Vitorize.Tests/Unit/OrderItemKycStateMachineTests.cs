using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests.Unit;

public sealed class OrderItemKycStateMachineTests
{
    [Theory]
    [InlineData(false, false, OrderItemKycStatus.NotRequired)]
    [InlineData(false, true, OrderItemKycStatus.NotRequired)]
    [InlineData(true, true, OrderItemKycStatus.Satisfied)]
    [InlineData(true, false, OrderItemKycStatus.AwaitingSubmission)]
    public void Initial_state_comes_only_from_the_explicit_requirement_and_satisfaction_inputs(
        bool requiresKyc, bool alreadySatisfied, OrderItemKycStatus expected)
    {
        Assert.Equal(expected, OrderItemKycStateMachine.CreateInitialState(requiresKyc, alreadySatisfied));
    }

    [Theory]
    [InlineData(OrderItemKycStatus.AwaitingSubmission, OrderItemKycStatus.AwaitingReview)]
    [InlineData(OrderItemKycStatus.AwaitingReview, OrderItemKycStatus.Satisfied)]
    [InlineData(OrderItemKycStatus.AwaitingReview, OrderItemKycStatus.Rejected)]
    [InlineData(OrderItemKycStatus.AwaitingReview, OrderItemKycStatus.FinalRejected)]
    [InlineData(OrderItemKycStatus.Rejected, OrderItemKycStatus.AwaitingReview)]
    [InlineData(OrderItemKycStatus.Rejected, OrderItemKycStatus.FinalRejected)]
    [InlineData(OrderItemKycStatus.AwaitingSubmission, OrderItemKycStatus.Expired)]
    [InlineData(OrderItemKycStatus.Rejected, OrderItemKycStatus.Expired)]
    [InlineData(OrderItemKycStatus.Expired, OrderItemKycStatus.AwaitingSubmission)]
    [InlineData(OrderItemKycStatus.Expired, OrderItemKycStatus.FinalRejected)]
    public void Valid_transitions_are_allowed(OrderItemKycStatus from, OrderItemKycStatus to)
    {
        Assert.True(OrderItemKycStateMachine.CanTransition(from, to));
        OrderItemKycStateMachine.EnsureTransition(from, to);
    }

    public static IEnumerable<object[]> InvalidTransitions() =>
        Enum.GetValues<OrderItemKycStatus>()
            .SelectMany(from => Enum.GetValues<OrderItemKycStatus>()
                .Where(to => !OrderItemKycStateMachine.CanTransition(from, to))
                .Select(to => new object[] { from, to }));

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public void Every_invalid_transition_including_all_terminal_transitions_is_rejected(OrderItemKycStatus from, OrderItemKycStatus to)
    {
        Assert.False(OrderItemKycStateMachine.CanTransition(from, to));
        Assert.Throws<InvalidOperationException>(() => OrderItemKycStateMachine.EnsureTransition(from, to));
    }

    [Theory]
    [InlineData(OrderItemKycStatus.NotRequired, false)]
    [InlineData(OrderItemKycStatus.Satisfied, false)]
    [InlineData(OrderItemKycStatus.AwaitingSubmission, true)]
    [InlineData(OrderItemKycStatus.AwaitingReview, true)]
    [InlineData(OrderItemKycStatus.Rejected, true)]
    [InlineData(OrderItemKycStatus.FinalRejected, true)]
    [InlineData(OrderItemKycStatus.Expired, true)]
    public void Fulfillment_block_is_derived_from_status_only(OrderItemKycStatus status, bool expected)
    {
        Assert.Equal(expected, OrderItemKycStateMachine.BlocksFulfillment(status));
    }

    [Fact]
    public void Unknown_statuses_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderItemKycStateMachine.EnsureTransition((OrderItemKycStatus)99, OrderItemKycStatus.Satisfied));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            OrderItemKycStateMachine.BlocksFulfillment((OrderItemKycStatus)99));
    }
}
