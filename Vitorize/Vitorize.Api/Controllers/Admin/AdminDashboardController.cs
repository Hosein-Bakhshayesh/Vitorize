using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Dashboard;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminDashboardController : ControllerBase
    {
        private readonly IAdminDashboardService _dashboardService;

        public AdminDashboardController(IAdminDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet]
        public async Task<ApiResult<DashboardDto>> Get()
        {
            var result = await _dashboardService.GetDashboardAsync();

            return ApiResult<DashboardDto>.Success(
                result,
                "اطلاعات داشبورد با موفقیت دریافت شد.");
        }
    }
}