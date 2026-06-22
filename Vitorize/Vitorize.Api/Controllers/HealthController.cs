using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    public class HealthController : ControllerBase
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ISettingService _settingService;

        public HealthController(
            VitorizeDbContext dbContext,
            ISettingService settingService)
        {
            _dbContext = dbContext;
            _settingService = settingService;
        }

        [HttpGet]
        public async Task<IActionResult> Check()
        {
            var result = new
            {
                Application = "Vitorize",
                Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"),
                ServerTime = DateTime.UtcNow,

                Database = await CheckDatabase(),

                Settings = await CheckSettings(),

                Payment = await CheckPayment(),

                Status = "Healthy"
            };

            return Ok(result);
        }

        [HttpGet("db")]
        public async Task<IActionResult> CheckDatabaseOnly()
        {
            return Ok(await CheckDatabase());
        }

        private async Task<object> CheckDatabase()
        {
            try
            {
                var canConnect =
                    await _dbContext.Database.CanConnectAsync();

                var productCount =
                    await _dbContext.Products.CountAsync();

                return new
                {
                    Healthy = canConnect,
                    ProductCount = productCount
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Healthy = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<object> CheckSettings()
        {
            try
            {
                var siteName =
                    await _settingService.GetValueAsync("SiteName");

                return new
                {
                    Healthy = !string.IsNullOrWhiteSpace(siteName),
                    SiteName = siteName
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Healthy = false,
                    Error = ex.Message
                };
            }
        }

        private async Task<object> CheckPayment()
        {
            try
            {
                var merchantId =
                    await _settingService.GetValueAsync("ZarinpalMerchantId");

                var sandbox =
                    await _settingService.GetValueAsync("ZarinpalSandbox");

                return new
                {
                    Healthy = !string.IsNullOrWhiteSpace(merchantId),
                    MerchantConfigured =
                        !string.IsNullOrWhiteSpace(merchantId),

                    Sandbox = sandbox
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    Healthy = false,
                    Error = ex.Message
                };
            }
        }
    }
}