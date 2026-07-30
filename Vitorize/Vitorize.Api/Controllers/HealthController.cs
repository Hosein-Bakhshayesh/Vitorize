using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Swashbuckle.AspNetCore.Annotations;
using Vitorize.Application.Interfaces;
using Vitorize.Api.Services;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Logging;

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
        private readonly IReadinessProbe _readinessProbe;
        private readonly ILogger<HealthController> _logger;

        public HealthController(
            VitorizeDbContext dbContext,
            ISettingService settingService,
            IZarinpalPaymentConfigurationProvider paymentConfiguration,
            IReadinessProbe readinessProbe,
            ILogger<HealthController> logger)
        {
            _dbContext = dbContext;
            _settingService = settingService;
            _paymentConfiguration = paymentConfiguration;
            _readinessProbe = readinessProbe;
            _logger = logger;
        }

        [HttpGet]
        [SwaggerOperation(
            Summary = "وضعیت کلی سیستم",
            Description = "بررسی سلامت API، دیتابیس، تنظیمات اصلی و پیکربندی پرداخت.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> Check(CancellationToken cancellationToken)
        {
            var healthy = await _readinessProbe.IsReadyAsync(cancellationToken);
            var result = new { Status = healthy ? "Healthy" : "Unhealthy" };
            return healthy ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        [HttpGet("ready")]
        [SwaggerOperation(
            Summary = "Dependency readiness",
            Description = "Checks that the API can accept store traffic. The result is intentionally minimal and contains no dependency details.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Readiness(CancellationToken cancellationToken)
        {
            var ready = await _readinessProbe.IsReadyAsync(cancellationToken);
            var result = new { Status = ready ? "Ready" : "NotReady" };
            return ready ? Ok(result) : StatusCode(StatusCodes.Status503ServiceUnavailable, result);
        }

        [HttpGet("details")]
        [Authorize(Policy = "SecurityDiagnostics")]
        [SwaggerOperation(
            Summary = "وضعیت دیتابیس",
            Description = "بررسی اتصال به SQL Server و دریافت یک آمار ساده از محصولات.")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> CheckDatabaseOnly(CancellationToken cancellationToken)
        {
            _logger.LogInformation(
                "Protected diagnostics were viewed. EventType={EventType}",
                OperationalEventNames.DiagnosticsViewed);

            return Ok(new
            {
                Database = await CheckDatabase(cancellationToken),
                Settings = await CheckSettings(),
                Payment = await CheckPayment(cancellationToken),
                ServerTime = DateTime.UtcNow
            });
        }

        private async Task<object> CheckDatabase(CancellationToken cancellationToken)
        {
            try
            {
                var canConnect = await _readinessProbe.IsReadyAsync(cancellationToken);
                if (!canConnect)
                    return new { Healthy = false, Error = "Database health check failed." };

                var productCount = await _dbContext.Products.CountAsync(cancellationToken);

                return new
                {
                    Healthy = canConnect,
                    ProductCount = productCount
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Protected database diagnostics failed. ExceptionType={ExceptionType} EventType={EventType}",
                    exception.GetType().Name,
                    OperationalEventNames.ReadinessProbeFailed);
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
                var siteName = await _settingService.GetValueAsync("SiteName");

                return new
                {
                    Healthy = !string.IsNullOrWhiteSpace(siteName),
                    SiteNameConfigured = !string.IsNullOrWhiteSpace(siteName)
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

        private async Task<object> CheckPayment(CancellationToken cancellationToken)
        {
            try
            {
                var configuration = await _paymentConfiguration.GetAsync(cancellationToken);
                var validation = await _paymentConfiguration.ValidateAsync(cancellationToken);

                return new
                {
                    Healthy = validation.IsValid,
                    MerchantConfigured = !string.IsNullOrWhiteSpace(configuration.MerchantId),
                    Sandbox = configuration.IsSandbox,
                    Error = validation.IsValid ? null : "Payment configuration is invalid."
                };
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    "Protected payment diagnostics failed. ExceptionType={ExceptionType} EventType={EventType}",
                    exception.GetType().Name,
                    OperationalEventNames.ReadinessProbeFailed);
                return new
                {
                    Healthy = false,
                    Error = "Payment health check failed."
                };
            }
        }
    }
}
