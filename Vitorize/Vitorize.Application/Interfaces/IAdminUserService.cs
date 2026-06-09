using Vitorize.Application.DTOs.Admin.Users;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminUserService
    {
        Task<PagedResult<AdminUserDto>> GetAllAsync(
            AdminUserFilterDto filter);

        Task<AdminUserDetailDto> GetByIdAsync(Guid userId);

        Task ActivateAsync(Guid userId);

        Task SuspendAsync(Guid userId);

        Task BlockAsync(Guid userId);

        Task AddRoleAsync(
            Guid userId,
            string roleName);

        Task RemoveRoleAsync(
            Guid userId,
            string roleName);
    }
}