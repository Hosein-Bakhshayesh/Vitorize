using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Orders;

namespace Vitorize.Application.Interfaces
{
    public interface IOrderService
    {
        Task<List<OrderDto>> GetMyOrdersAsync(Guid userId);

        Task<OrderDto> GetMyOrderDetailsAsync(Guid userId, Guid orderId);

        Task<List<DeliveredCodeDto>> GetMyDeliveredCodesAsync(Guid userId);

        Task<List<OrderDto>> GetAdminOrdersAsync();

        Task<OrderDto> GetAdminOrderDetailsAsync(Guid orderId);

        Task<List<OrderDto>> SearchAdminOrdersAsync(AdminOrderFilterDto filter);

        Task CancelOrderAsync(Guid orderId, Guid adminUserId, string? reason);

        Task CompleteOrderAsync(Guid orderId, Guid adminUserId);

        Task DeliverManualAsync(Guid orderId, Guid adminUserId, ManualDeliveryRequestDto request);
    }
}
