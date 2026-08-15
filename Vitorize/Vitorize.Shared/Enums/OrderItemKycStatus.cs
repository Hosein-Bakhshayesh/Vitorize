namespace Vitorize.Shared.Enums;

/// <summary>
/// Mutable, item-level lifecycle state for post-payment KYC fulfillment gating.
/// This is intentionally independent from order and payment status.
/// </summary>
public enum OrderItemKycStatus : byte
{
    NotRequired = 1,
    Satisfied = 2,
    AwaitingSubmission = 3,
    AwaitingReview = 4,
    Rejected = 5,
    FinalRejected = 6,
    Expired = 7
}
