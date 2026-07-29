using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/error-logs")]
    public class AdminErrorLogsController : ControllerBase
    {
        private readonly IAdminSystemReadService _service;
        public AdminErrorLogsController(IAdminSystemReadService service) => _service = service;
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminErrorLogDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetErrorLogsAsync(filter);
            return Ok(ApiResult<List<AdminErrorLogDto>>.Success(result, "اطلاعات با موفقیت دریافت شد."));
        }
        [HttpGet("paged")]
        public async Task<ActionResult<ApiResult<PagedResult<AdminErrorLogDto>>>> GetPaged([FromQuery] AdminQueryFilterDto filter, CancellationToken cancellationToken)
        {
            var result = await _service.GetPagedErrorLogsAsync(filter, cancellationToken);
            return Ok(ApiResult<PagedResult<AdminErrorLogDto>>.Success(result, "فهرست صفحه‌بندی‌شده خطاها دریافت شد."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminErrorLogDto>>> GetById(Guid id)
        {
            var result = await _service.GetErrorLogByIdAsync(id);
            return Ok(ApiResult<AdminErrorLogDto>.Success(result, "جزئیات با موفقیت دریافت شد."));
        }
    }
}
