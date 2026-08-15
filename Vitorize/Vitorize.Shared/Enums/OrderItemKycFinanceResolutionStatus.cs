namespace Vitorize.Shared.Enums;

/// <summary>
/// An explicit, item-scoped financial decision following terminal KYC rejection.
/// No value implies that a refund has been issued automatically.
/// </summary>
public enum OrderItemKycFinanceResolutionStatus : byte
{
    Pending = 1,
    ResolvedExternalRefund = 2,
    ResolvedNoRefund = 3
}
