using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/seed")]
    public class AdminSeedController : ControllerBase
    {
        private readonly IVitorizeSeedService _seedService;
        public AdminSeedController(IVitorizeSeedService seedService) => _seedService = seedService;
        [HttpPost("run")]
        public async Task<ActionResult<ApiResult>> Run()
        {
            await _seedService.SeedAsync();
            return Ok(ApiResult.Success("اطلاعات اولیه با موفقیت آماده شد."));
        }
    }
}
