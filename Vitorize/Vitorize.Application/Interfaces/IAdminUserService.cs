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

        /// <summary>
        /// Replaces a user's password on their behalf and ends every session they hold.
        /// Returns how many sessions were revoked, so the caller can say so plainly.
        /// </summary>
        Task<int> ResetPasswordAsync(Guid userId, string newPassword, string confirmPassword);

        Task AddRoleAsync(
            Guid userId,
            string roleName);

        Task RemoveRoleAsync(
            Guid userId,
            string roleName);
    }
}