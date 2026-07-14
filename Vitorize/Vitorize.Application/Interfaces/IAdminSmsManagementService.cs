using Vitorize.Application.DTOs.Admin.Sms;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminSmsManagementService
    {
        Task<PagedResult<SmsHistoryItemDto>> GetHistoryAsync(SmsHistoryFilterDto filter, bool allowFullMobile, CancellationToken cancellationToken = default);
        Task<SmsHistoryItemDto> GetByIdAsync(Guid id, bool allowFullMobile, CancellationToken cancellationToken = default);
        Task<SmsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);
        Task<SmsHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
        Task<bool> CanViewFullMobileAsync(CancellationToken cancellationToken = default);
        Task<SmsActionResultDto> SendNotificationAsync(SendCustomNotificationRequestDto request, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<SmsActionResultDto> SendTextAsync(SendCustomTextRequestDto request, Guid adminUserId, CancellationToken cancellationToken = default);
        Task RetryAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default);
        Task CancelAsync(Guid id, Guid adminUserId, CancellationToken cancellationToken = default);
        Task<string> ExportCsvAsync(SmsHistoryFilterDto filter, CancellationToken cancellationToken = default);
    }
}
