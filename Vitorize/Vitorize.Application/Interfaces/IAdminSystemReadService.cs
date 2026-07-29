using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminSystemReadService
    {
        Task<List<AdminErrorLogDto>> GetErrorLogsAsync(AdminQueryFilterDto filter);
        Task<Vitorize.Shared.Common.PagedResult<AdminErrorLogDto>> GetPagedErrorLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default);
        Task<AdminErrorLogDto> GetErrorLogByIdAsync(Guid id);
        Task<List<AdminAuditLogDto>> GetAuditLogsAsync(AdminQueryFilterDto filter);
        Task<Vitorize.Shared.Common.PagedResult<AdminAuditLogDto>> GetPagedAuditLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default);
        Task<AdminAuditLogDto> GetAuditLogByIdAsync(Guid id);
        Task<List<AdminSecurityLogDto>> GetSecurityLogsAsync(AdminQueryFilterDto filter);
        Task<Vitorize.Shared.Common.PagedResult<AdminSecurityLogDto>> GetPagedSecurityLogsAsync(AdminQueryFilterDto filter, CancellationToken cancellationToken = default);
        Task<AdminSecurityLogDto> GetSecurityLogByIdAsync(Guid id);
    }
}
