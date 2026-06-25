using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminSystemReadService
    {
        Task<List<AdminErrorLogDto>> GetErrorLogsAsync(AdminQueryFilterDto filter);
        Task<AdminErrorLogDto> GetErrorLogByIdAsync(Guid id);
        Task<List<AdminAuditLogDto>> GetAuditLogsAsync(AdminQueryFilterDto filter);
        Task<AdminAuditLogDto> GetAuditLogByIdAsync(Guid id);
        Task<List<AdminSecurityLogDto>> GetSecurityLogsAsync(AdminQueryFilterDto filter);
        Task<AdminSecurityLogDto> GetSecurityLogByIdAsync(Guid id);
    }
}
