using Vitorize.Application.DTOs.Verification;

namespace Vitorize.Application.Interfaces;

public interface IOrderItemKycFinanceResolutionService
{
    Task EnsurePendingAsync(Guid orderItemId, CancellationToken cancellationToken = default);
    Task<OrderItemKycFinanceResolutionDto?> GetForOrderItemAsync(Guid orderItemId, CancellationToken cancellationToken = default);
    Task<OrderItemKycFinanceResolutionDto> ResolveExternalAsync(Guid orderItemId, Guid actorUserId,
        ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken = default);
    Task<OrderItemKycFinanceResolutionDto> ResolveNoRefundAsync(Guid orderItemId, Guid actorUserId,
        ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken = default);
}
