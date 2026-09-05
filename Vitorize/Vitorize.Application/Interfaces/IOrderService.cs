using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Orders;

namespace Vitorize.Application.Interfaces
{
    public interface IOrderService
    {
        Task<List<OrderDto>> GetMyOrdersAsync(Guid userId);

        Task<OrderDto> GetMyOrderDetailsAsync(Guid userId, Guid orderId);

        Task<OrderItemKycProjectionDto> GetMyOrderItemKycContextAsync(Guid userId, Guid orderItemId);

        Task<List<DeliveredCodeDto>> GetMyDeliveredCodesAsync(Guid userId);

        Task<List<OrderDto>> GetAdminOrdersAsync();

        Task<OrderDto> GetAdminOrderDetailsAsync(Guid orderId);

        Task<List<OrderDto>> SearchAdminOrdersAsync(AdminOrderFilterDto filter);
        Task<AdminOrderPagedResultDto> GetPagedAdminOrdersAsync(AdminOrderFilterDto filter, CancellationToken cancellationToken = default);
        Task<List<OrderDto>> GetSelectedAdminOrdersForExportAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);

        Task CancelOrderAsync(Guid orderId, Guid adminUserId, string? reason);

        /// <summary>
        /// Cancels an order on behalf of the customer who owns it. Ownership and cancellability are
        /// both decided server-side; nothing is deleted.
        /// </summary>
        Task<OrderDto> CancelMyOrderAsync(Guid userId, Guid orderId);

        /// <summary>
        /// Hides a settled, never-paid order from the owning customer's own list. Presentation only:
        /// the order stays intact and fully visible to Admin.
        /// </summary>
        Task HideMyOrderAsync(Guid userId, Guid orderId);

        Task CompleteOrderAsync(Guid orderId, Guid adminUserId);

        Task DeliverManualAsync(Guid orderId, Guid adminUserId, ManualDeliveryRequestDto request);
    }
}
