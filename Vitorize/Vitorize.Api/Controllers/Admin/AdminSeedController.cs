using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "SuperAdminOnly")]
    [Route("api/admin/seed")]
    public class AdminSeedController : ControllerBase
    {
        private readonly IVitorizeSeedService _seedService;
        private readonly ISecurityLogService _securityLogService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IHostEnvironment _environment;

        public AdminSeedController(
            IVitorizeSeedService seedService,
            ISecurityLogService securityLogService,
            ICurrentUserService currentUserService,
            IHostEnvironment environment)
        {
            _seedService = seedService;
            _securityLogService = securityLogService;
            _currentUserService = currentUserService;
            _environment = environment;
        }

        [HttpPost("run")]
        public async Task<ActionResult<ApiResult>> Run(CancellationToken cancellationToken)
        {
            if (!_environment.IsDevelopment())
                return NotFound();

            try
            {
                await _seedService.SeedReferenceDataAsync(cancellationToken);
                await _securityLogService.LogAsync(
                    _currentUserService.UserId,
                    "ReferenceDataSeedRun",
                    true,
                    "Reference data seeding was run from the Development-only Admin tool.",
                    _currentUserService.IpAddress,
                    _currentUserService.UserAgent);

                return Ok(ApiResult.Success("اطلاعات مرجع با موفقیت آماده شد."));
            }
            catch
            {
                await _securityLogService.LogAsync(
                    _currentUserService.UserId,
                    "ReferenceDataSeedRun",
                    false,
                    "Reference data seeding failed in the Development-only Admin tool.",
                    _currentUserService.IpAddress,
                    _currentUserService.UserAgent);

                throw;
            }
        }
    }
}
