using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin;

[ApiController]
[Authorize(Policy = "SecurityDiagnostics")]
[Route("api/admin/monitoring")]
public sealed class AdminMonitoringController : ControllerBase
{
    private readonly IAdminMonitoringService _monitoring;

    public AdminMonitoringController(IAdminMonitoringService monitoring) => _monitoring = monitoring;

    [HttpGet]
    public async Task<ActionResult<ApiResult<AdminMonitoringDto>>> Get(CancellationToken cancellationToken)
    {
        var result = await _monitoring.GetAsync(cancellationToken);
        return Ok(ApiResult<AdminMonitoringDto>.Success(result, "وضعیت عملیاتی دریافت شد."));
    }
}
