using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminPaymentReadService
    {
        Task<List<AdminPaymentDto>> GetAllAsync(AdminQueryFilterDto filter);
        Task<Vitorize.Shared.Common.PagedResult<AdminPaymentDto>> GetPagedAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default);
        Task<AdminPaymentDto> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<Vitorize.Shared.Common.PagedResult<Vitorize.Application.DTOs.Payments.PaymentRefundDto>> GetRefundsPagedAsync(Guid paymentId, PaymentDetailHistoryFilterDto filter, CancellationToken cancellationToken = default);
        Task<Vitorize.Shared.Common.PagedResult<FinancialAuditEntryDto>> GetAuditHistoryPagedAsync(Guid paymentId, PaymentDetailHistoryFilterDto filter, CancellationToken cancellationToken = default);
    }
}
