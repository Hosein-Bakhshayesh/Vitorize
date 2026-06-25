using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/audit-logs")]
    public class AdminAuditLogsController : ControllerBase
    {
        private readonly IAdminSystemReadService _service;
        public AdminAuditLogsController(IAdminSystemReadService service) => _service = service;
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminAuditLogDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetAuditLogsAsync(filter);
            return Ok(ApiResult<List<AdminAuditLogDto>>.Success(result, "اطلاعات با موفقیت دریافت شد."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminAuditLogDto>>> GetById(Guid id)
        {
            var result = await _service.GetAuditLogByIdAsync(id);
            return Ok(ApiResult<AdminAuditLogDto>.Success(result, "جزئیات با موفقیت دریافت شد."));
        }
    }
}
