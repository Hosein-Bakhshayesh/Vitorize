using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminNotificationReadService
    {
        Task<List<AdminNotificationDto>> GetAllAsync(AdminQueryFilterDto filter);
        Task<AdminNotificationDto> GetByIdAsync(Guid id);
        Task MarkAsReadAsync(Guid id);
    }
}
