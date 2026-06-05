namespace Vitorize.Application.Interfaces
{
    public interface IGiftCodeDeliveryService
    {
        Task DeliverOrderAsync(Guid orderId, Guid? deliveredByUserId = null);
    }
}