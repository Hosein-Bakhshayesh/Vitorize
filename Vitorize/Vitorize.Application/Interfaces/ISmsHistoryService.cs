using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    public interface ISmsHistoryService
    {
        Task<Guid> CreatePendingAsync(SmsHistoryRecordRequest request, CancellationToken cancellationToken = default);
        Task<Guid> RecordDirectResultAsync(SmsHistoryRecordRequest request, SmsSendResult result, CancellationToken cancellationToken = default);
    }
}
