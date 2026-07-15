using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Users;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Policy = "UserManage")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _adminUserService;

        public AdminUsersController(
            IAdminUserService adminUserService)
        {
            _adminUserService = adminUserService;
        }

        [HttpGet]
        public async Task<ApiResult<
            PagedResult<AdminUserDto>>> GetAll(
            [FromQuery] AdminUserFilterDto filter)
        {
            var result =
                await _adminUserService
                    .GetAllAsync(filter);

            return ApiResult<
                PagedResult<AdminUserDto>>
                .Success(result);
        }
        [HttpGet("{id:guid}")]
        public async Task<ApiResult<AdminUserDetailDto>> GetById(
            Guid id)
        {
            var result = await _adminUserService.GetByIdAsync(id);

            return ApiResult<AdminUserDetailDto>
                .Success(result);
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<ApiResult> Activate(
            Guid id)
        {
            await _adminUserService.ActivateAsync(id);

            return ApiResult.Success(
                "کاربر فعال شد.");
        }

        [HttpPost("{id:guid}/suspend")]
        public async Task<ApiResult> Suspend(
            Guid id)
        {
            await _adminUserService.SuspendAsync(id);

            return ApiResult.Success(
                "کاربر معلق شد.");
        }

        [HttpPost("{id:guid}/block")]
        public async Task<ApiResult> Block(
            Guid id)
        {
            await _adminUserService.BlockAsync(id);

            return ApiResult.Success(
                "کاربر مسدود شد.");
        }

        [HttpPost("{id:guid}/roles/add")]
        public async Task<ApiResult> AddRole(
            Guid id,
            [FromBody] UpdateUserRoleDto request)
        {
            await _adminUserService.AddRoleAsync(
                id,
                request.RoleName);

            return ApiResult.Success(
                "نقش اضافه شد.");
        }

        [HttpPost("{id:guid}/roles/remove")]
        public async Task<ApiResult> RemoveRole(
            Guid id,
            [FromBody] UpdateUserRoleDto request)
        {
            await _adminUserService.RemoveRoleAsync(
                id,
                request.RoleName);

            return ApiResult.Success(
                "نقش حذف شد.");
        }
    }
}
