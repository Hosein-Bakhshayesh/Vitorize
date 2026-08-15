using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// Pure transition policy for the mutable item-level KYC lifecycle. Persistence
/// and fulfillment orchestration deliberately live outside this policy.
/// </summary>
public static class OrderItemKycStateMachine
{
    public static OrderItemKycStatus CreateInitialState(bool requiresKyc, bool verificationAlreadySatisfied) =>
        !requiresKyc
            ? OrderItemKycStatus.NotRequired
            : verificationAlreadySatisfied
                ? OrderItemKycStatus.Satisfied
                : OrderItemKycStatus.AwaitingSubmission;

    public static bool CanTransition(OrderItemKycStatus from, OrderItemKycStatus to) =>
        from switch
        {
            OrderItemKycStatus.AwaitingSubmission => to is OrderItemKycStatus.AwaitingReview or OrderItemKycStatus.Expired,
            OrderItemKycStatus.AwaitingReview => to is OrderItemKycStatus.Satisfied
                or OrderItemKycStatus.Rejected
                or OrderItemKycStatus.FinalRejected,
            // Profile resubmission returns the existing verification profile to
            // Pending, so the item re-enters review directly rather than inventing
            // a second submission state.
            OrderItemKycStatus.Rejected => to is OrderItemKycStatus.AwaitingReview
                or OrderItemKycStatus.FinalRejected or OrderItemKycStatus.Expired,
            OrderItemKycStatus.Expired => to is OrderItemKycStatus.AwaitingSubmission
                or OrderItemKycStatus.FinalRejected,
            _ => false
        };

    public static void EnsureTransition(OrderItemKycStatus from, OrderItemKycStatus to)
    {
        if (!Enum.IsDefined(from))
            throw new ArgumentOutOfRangeException(nameof(from), from, "Unknown item KYC status.");
        if (!Enum.IsDefined(to))
            throw new ArgumentOutOfRangeException(nameof(to), to, "Unknown item KYC status.");
        if (!CanTransition(from, to))
            throw new InvalidOperationException($"KYC transition from {from} to {to} is not allowed.");
    }

    public static bool BlocksFulfillment(OrderItemKycStatus status) =>
        status switch
        {
            OrderItemKycStatus.NotRequired or OrderItemKycStatus.Satisfied => false,
            OrderItemKycStatus.AwaitingSubmission or OrderItemKycStatus.AwaitingReview
                or OrderItemKycStatus.Rejected or OrderItemKycStatus.FinalRejected
                or OrderItemKycStatus.Expired => true,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown item KYC status.")
        };
}
