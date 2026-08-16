using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminNotificationBroadcastService
    {
        Task<BroadcastPreviewResultDto> PreviewAsync(
            BroadcastPreviewRequestDto request,
            CancellationToken cancellationToken = default);

        Task<BroadcastDto> SendAsync(
            Guid actorUserId,
            SendBroadcastRequestDto request,
            CancellationToken cancellationToken = default);

        Task<PagedResult<BroadcastDto>> GetHistoryAsync(
            int page,
            int pageSize,
            CancellationToken cancellationToken = default);

        Task<BroadcastDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
