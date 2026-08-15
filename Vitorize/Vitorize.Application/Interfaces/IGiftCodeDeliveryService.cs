namespace Vitorize.Application.Interfaces
{
    public interface IGiftCodeDeliveryService
    {
        Task DeliverOrderAsync(Guid orderId, Guid? deliveredByUserId = null);

        /// <summary>Delivers the already paid-allocated codes for one item.</summary>
        Task<bool> DeliverSatisfiedOrderItemAsync(Guid orderItemId, Guid? deliveredByUserId = null, CancellationToken cancellationToken = default);
    }
}
