using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminPaymentReadService
    {
        Task<List<AdminPaymentDto>> GetAllAsync(AdminQueryFilterDto filter);
        Task<AdminPaymentDto> GetByIdAsync(Guid id);
    }
}
