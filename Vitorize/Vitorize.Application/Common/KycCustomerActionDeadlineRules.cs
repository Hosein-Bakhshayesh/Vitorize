using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// Central policy for persisted customer-action KYC deadlines. Expiry execution
/// deliberately belongs outside this pure rule set.
/// </summary>
public static class KycCustomerActionDeadlineRules
{
    public static void EnsureValidDuration(int? durationHours)
    {
        if (durationHours is <= 0)
            throw new ArgumentOutOfRangeException(nameof(durationHours), "KYC customer-action deadline hours must be positive when configured.");
    }

    public static DateTime? CalculateInitialDeadline(DateTime paidAtUtc, int? durationHours) =>
        CalculateDeadline(paidAtUtc, durationHours);

    public static DateTime? CalculateRejectedDeadline(DateTime rejectedAtUtc, int? durationHours) =>
        CalculateDeadline(rejectedAtUtc, durationHours);

    public static bool AppliesTo(OrderItemKycStatus status) =>
        status is OrderItemKycStatus.AwaitingSubmission or OrderItemKycStatus.Rejected;

    public static bool IsOverdue(OrderItemKycStatus status, DateTime? deadlineAtUtc, DateTime utcNow) =>
        AppliesTo(status) && deadlineAtUtc.HasValue && utcNow >= deadlineAtUtc.Value;

    public static DateTime? DeadlineAfterTransition(OrderItemKycStatus target, int? durationHours, DateTime transitionAtUtc) =>
        target == OrderItemKycStatus.Rejected
            ? CalculateRejectedDeadline(transitionAtUtc, durationHours)
            : null;

    private static DateTime? CalculateDeadline(DateTime startsAtUtc, int? durationHours)
    {
        EnsureValidDuration(durationHours);
        return durationHours.HasValue
            ? DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc).AddHours(durationHours.Value)
            : null;
    }
}
