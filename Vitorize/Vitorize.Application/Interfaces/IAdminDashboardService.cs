using Vitorize.Application.DTOs.Admin.Dashboard;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminDashboardService
    {
        Task<DashboardDto> GetDashboardAsync();
    }
}