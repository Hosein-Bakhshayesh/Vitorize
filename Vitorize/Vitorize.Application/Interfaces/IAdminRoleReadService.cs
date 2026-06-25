using Vitorize.Application.DTOs.Admin.Roles;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminRoleReadService
    {
        Task<List<AdminRoleDto>> GetAllAsync();
        Task<AdminRoleDto> GetByIdAsync(Guid id);
    }
}
