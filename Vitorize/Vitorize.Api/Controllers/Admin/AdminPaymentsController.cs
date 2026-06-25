using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/payments")]
    public class AdminPaymentsController : ControllerBase
    {
        private readonly IAdminPaymentReadService _service;
        public AdminPaymentsController(IAdminPaymentReadService service) => _service = service;
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminPaymentDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetAllAsync(filter);
            return Ok(ApiResult<List<AdminPaymentDto>>.Success(result, "پرداخت‌ها با موفقیت دریافت شدند."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminPaymentDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResult<AdminPaymentDto>.Success(result, "جزئیات پرداخت با موفقیت دریافت شد."));
        }
    }
}
