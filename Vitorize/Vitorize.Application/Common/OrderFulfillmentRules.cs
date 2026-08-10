using Vitorize.Shared.Enums;

namespace Vitorize.Application.Common;

/// <summary>Small shared invariant for the existing order-fulfilment workflow.</summary>
public static class OrderFulfillmentRules
{
    public static bool IsPaid(byte paymentStatus) => paymentStatus == (byte)PaymentStatus.Paid;

    public static bool IsFullyFulfilled(IEnumerable<byte> deliveryStatuses) =>
        deliveryStatuses.All(status => status == (byte)DeliveryStatus.Delivered);

    public static bool CanComplete(byte paymentStatus, IEnumerable<byte> deliveryStatuses) =>
        IsPaid(paymentStatus) && IsFullyFulfilled(deliveryStatuses);

    public static int OutstandingCount(IEnumerable<byte> deliveryStatuses) =>
        deliveryStatuses.Count(status => status != (byte)DeliveryStatus.Delivered);
}
