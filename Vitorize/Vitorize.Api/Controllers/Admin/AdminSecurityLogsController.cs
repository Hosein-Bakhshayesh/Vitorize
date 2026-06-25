using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/security-logs")]
    public class AdminSecurityLogsController : ControllerBase
    {
        private readonly IAdminSystemReadService _service;
        public AdminSecurityLogsController(IAdminSystemReadService service) => _service = service;
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminSecurityLogDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetSecurityLogsAsync(filter);
            return Ok(ApiResult<List<AdminSecurityLogDto>>.Success(result, "اطلاعات با موفقیت دریافت شد."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminSecurityLogDto>>> GetById(Guid id)
        {
            var result = await _service.GetSecurityLogByIdAsync(id);
            return Ok(ApiResult<AdminSecurityLogDto>.Success(result, "جزئیات با موفقیت دریافت شد."));
        }
    }
}
