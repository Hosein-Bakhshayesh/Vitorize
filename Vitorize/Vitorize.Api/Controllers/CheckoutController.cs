using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    public class CheckoutController : ControllerBase
    {
        private readonly ICheckoutService _checkoutService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IIdempotencyService _idempotencyService;

        public CheckoutController(
            ICheckoutService checkoutService,
            ICurrentUserService currentUserService,
            IIdempotencyService idempotencyService)
        {
            _checkoutService = checkoutService;
            _currentUserService = currentUserService;
            _idempotencyService = idempotencyService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<CheckoutResultDto>>> Checkout(
            CheckoutRequestDto request)
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var userId = _currentUserService.UserId.Value;

            var idempotencyKey =
                Request.Headers["Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var requestHash = RequestHashHelper.ComputeHash(request);

            await _idempotencyService.StartAsync(
                userId,
                idempotencyKey,
                requestHash);

            try
            {
                var result = await _checkoutService.CheckoutAsync(
                    userId,
                    request);

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