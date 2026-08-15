using Vitorize.Application.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests.Unit;

public sealed class KycCustomerActionDeadlineRulesTests
{
    private static readonly DateTime Start = new(2026, 8, 14, 9, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Null_duration_has_no_deadline()
    {
        Assert.Null(KycCustomerActionDeadlineRules.CalculateInitialDeadline(Start, null));
    }

    [Fact]
    public void Initial_deadline_uses_the_authoritative_start_time_and_duration()
    {
        Assert.Equal(Start.AddHours(48), KycCustomerActionDeadlineRules.CalculateInitialDeadline(Start, 48));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public void Non_positive_duration_is_rejected(int hours)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => KycCustomerActionDeadlineRules.EnsureValidDuration(hours));
    }

    [Fact]
    public void Deadline_boundary_is_inclusively_overdue_only_while_customer_action_is_required()
    {
        var deadline = Start.AddHours(48);

        Assert.False(KycCustomerActionDeadlineRules.IsOverdue(OrderItemKycStatus.AwaitingSubmission, deadline, deadline.AddTicks(-1)));
        Assert.True(KycCustomerActionDeadlineRules.IsOverdue(OrderItemKycStatus.AwaitingSubmission, deadline, deadline));
        Assert.True(KycCustomerActionDeadlineRules.IsOverdue(OrderItemKycStatus.Rejected, deadline, deadline.AddTicks(1)));
        Assert.False(KycCustomerActionDeadlineRules.IsOverdue(OrderItemKycStatus.AwaitingReview, deadline, deadline.AddDays(1)));
    }

    [Fact]
    public void Review_satisfaction_and_final_rejection_clear_the_active_deadline()
    {
        Assert.Null(KycCustomerActionDeadlineRules.DeadlineAfterTransition(OrderItemKycStatus.AwaitingReview, 48, Start));
        Assert.Null(KycCustomerActionDeadlineRules.DeadlineAfterTransition(OrderItemKycStatus.Satisfied, 48, Start));
        Assert.Null(KycCustomerActionDeadlineRules.DeadlineAfterTransition(OrderItemKycStatus.FinalRejected, 48, Start));
    }

    [Fact]
    public void Rejection_creates_a_fresh_customer_action_window()
    {
        var rejectedAt = Start.AddDays(3);
        Assert.Equal(rejectedAt.AddHours(24),
            KycCustomerActionDeadlineRules.DeadlineAfterTransition(OrderItemKycStatus.Rejected, 24, rejectedAt));
    }
}
