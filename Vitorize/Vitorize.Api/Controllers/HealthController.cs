using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/health")]
    [SwaggerTag("Health check APIs for monitoring API, database, settings and payment configuration.")]
    public class HealthController : ControllerBase
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ISettingService _settingService;
        private readonly IZarinpalPaymentConfigurationProvider _paymentConfiguration;

        public HealthController(
            VitorizeDbContext dbContext,
            ISettingService settingService,
            IZarinpalPaymentConfigurationProvider paymentConfiguration)
        {
            _dbContext = dbContext;
            _settingService = settingService;
            _paymentConfiguration = paymentConfiguration;
        }

        [HttpGet]
        [SwaggerOperation(
            Summary = "وضعیت کلی سیستم",
            Description = "بررسی سلامت API، دیتابیس، تنظیمات اصلی و پیکربندی پرداخت.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Check()
        {
            var healthy = await _dbContext.Database.CanConnectAsync();
            var result = new { Status = healthy ? "Healthy" : "Unhealthy" };
            return healthy ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        [HttpGet("details")]
        [Authorize(Policy = "SecurityDiagnostics")]
        [SwaggerOperation(
            Summary = "وضعیت دیتابیس",
            Description = "بررسی اتصال به SQL Server و دریافت یک آمار ساده از محصولات.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckDatabaseOnly()
        {
            return Ok(new
            {
                Database = await CheckDatabase(),
                Settings = await CheckSettings(),
                Payment = await CheckPayment(),
                ServerTime = DateTime.UtcNow
            });
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
            catch
            {
                return new
                {
                    Healthy = false,
                    Error = "Database health check failed."
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
            catch
            {
                return new
                {
                    Healthy = false,
                    Error = "Settings health check failed."
                };
            }
        }

        private async Task<object> CheckPayment()
        {
            try
            {
                var configuration = await _paymentConfiguration.GetAsync();
                var validation = await _paymentConfiguration.ValidateAsync();

                return new
                {
                    Healthy = validation.IsValid,
                    MerchantConfigured = !string.IsNullOrWhiteSpace(configuration.MerchantId),
                    Sandbox = configuration.IsSandbox,
                    Errors = validation.IsValid ? Array.Empty<string>() : validation.Errors
                };
            }
            catch
            {
                return new
                {
                    Healthy = false,
                    Error = "Payment health check failed."
                };
            }
        }
    }
}
