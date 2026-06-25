using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Roles;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/roles")]
    public class AdminRolesController : ControllerBase
    {
        private readonly IAdminRoleReadService _service;
        public AdminRolesController(IAdminRoleReadService service) => _service = service;
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminRoleDto>>>> GetAll()
        {
            var result = await _service.GetAllAsync();
            return Ok(ApiResult<List<AdminRoleDto>>.Success(result, "نقش‌ها با موفقیت دریافت شدند."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminRoleDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResult<AdminRoleDto>.Success(result, "جزئیات نقش با موفقیت دریافت شد."));
        }
    }
}
