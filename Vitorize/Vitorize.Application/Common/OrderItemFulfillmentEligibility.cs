using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>
/// Backend fulfillment guard for Phase-2 KYC-managed items. Missing state is
/// intentionally backward compatible for historical/non-managed order items.
/// </summary>
public static class OrderItemFulfillmentEligibility
{
    public static bool CanFulfill(OrderItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item.KycLifecycleState is null ||
            !OrderItemKycStateMachine.BlocksFulfillment((OrderItemKycStatus)item.KycLifecycleState.Status);
    }

    public static void EnsureCanFulfill(OrderItem item)
    {
        if (!CanFulfill(item))
            throw new InvalidOperationException("این آیتم تا تکمیل احراز هویت قابل تحویل نیست.");
    }
}
