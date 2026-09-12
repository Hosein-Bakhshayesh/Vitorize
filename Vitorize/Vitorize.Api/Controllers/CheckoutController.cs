using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Swashbuckle.AspNetCore.Annotations;
using System.Text.Json;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Helpers;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [SwaggerTag("Checkout APIs for creating orders from cart and reserving gift codes.")]
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IIdempotencyService _idempotencyService;
        private readonly IOrderKycSettingsProvider _orderKycSettingsProvider;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<CheckoutController> _logger;

        public CheckoutController(
            ICheckoutService checkoutService,
            ICurrentUserService currentUserService,
            IIdempotencyService idempotencyService,
            IOrderKycSettingsProvider orderKycSettingsProvider,
            IServiceScopeFactory scopeFactory,
            ILogger<CheckoutController> logger)
        {
            _checkoutService = checkoutService;
            _currentUserService = currentUserService;
            _idempotencyService = idempotencyService;
            _orderKycSettingsProvider = orderKycSettingsProvider;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        [HttpGet("kyc-settings")]
        [ProducesResponseType(typeof(ApiResult<OrderKycSettingsDto>), StatusCodes.Status200OK)]
        public async Task<ActionResult<ApiResult<OrderKycSettingsDto>>> GetKycSettings()
        {
            var settings = await _orderKycSettingsProvider.GetAsync();
            return Ok(ApiResult<OrderKycSettingsDto>.Success(new OrderKycSettingsDto
            {
                ThresholdToman = settings.ThresholdToman,
                CustomerNotice = settings.CustomerNotice
            }));
        }

        [HttpPost]
        [SwaggerOperation(
     Summary = "ثبت سفارش",
     Description = "ایجاد سفارش از روی سبد خرید کاربر، اعمال کد تخفیف در صورت وجود، رزرو GiftCode و ایجاد پرداخت Pending. ارسال Header با نام Idempotency-Key الزامی است.")]
        [ProducesResponseType(typeof(ApiResult<CheckoutResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<CheckoutResultDto>>> Checkout(
     [FromBody] CheckoutRequestDto request,
     [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var userId = _currentUserService.UserId.Value;

            var requestHash = RequestHashHelper.ComputeHash(request);

            await _idempotencyService.StartAsync(
                userId,
                idempotencyKey,
                requestHash);

            try
            {
                var result = await Vitorize.Api.Services.SqlDeadlockRetry.ExecuteOnceAsync(
                    () => _checkoutService.CheckoutAsync(userId, request),
                    async () =>
                    {
                        await using var scope = _scopeFactory.CreateAsyncScope();
                        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
                            .CheckoutAsync(userId, request);
                    },
                    _logger,
                    "Checkout.CreateOrder",
                    HttpContext.RequestAborted);

                var response = ApiResult<CheckoutResultDto>.Success(
                    result,
                    "سفارش با موفقیت ایجاد شد.");

                await _idempotencyService.CompleteAsync(
                    idempotencyKey,
                    JsonSerializer.Serialize(response),
                    StatusCodes.Status200OK);

                return Ok(response);
            }
            catch (Exception ex)
            {
                await _idempotencyService.FailAsync(
                    idempotencyKey,
                    ex.Message);

                throw;
            }
        }
    }
}
