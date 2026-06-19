using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Helpers;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IIdempotencyService _idempotencyService;

        public PaymentsController(
            IPaymentService paymentService,
            ICurrentUserService currentUserService,
            IIdempotencyService idempotencyService)
        {
            _paymentService = paymentService;
            _currentUserService = currentUserService;
            _idempotencyService = idempotencyService;
        }

        [HttpPost("start/{orderId:guid}")]
        public async Task<ActionResult<ApiResult<PaymentStartResultDto>>> Start(Guid orderId)
        {
            var userId = GetUserId();

            var result = await _paymentService.StartPaymentAsync(userId, orderId);

            return Ok(ApiResult<PaymentStartResultDto>.Success(
                result,
                "پرداخت با موفقیت آماده شد."));
        }

        [HttpPost("mock/verify/{paymentId:guid}")]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> VerifyMock(Guid paymentId)
        {
            var userId = GetUserId();

            var result = await _paymentService.VerifyMockPaymentAsync(userId, paymentId);

            return Ok(ApiResult<PaymentVerifyResultDto>.Success(
                result,
                "پرداخت با موفقیت تایید شد."));
        }

        [HttpPost("wallet/pay/{orderId:guid}")]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> PayWithWallet(Guid orderId)
        {
            var userId = GetUserId();

            var idempotencyKey = Request.Headers["Idempotency-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var requestHash = RequestHashHelper.ComputeHash(new
            {
                UserId = userId,
                OrderId = orderId,
                Action = "WalletPay"
            });

            await _idempotencyService.StartAsync(userId, idempotencyKey, requestHash);

            try
            {
                var result = await _paymentService.PayWithWalletAsync(userId, orderId);

                var response = ApiResult<PaymentVerifyResultDto>.Success(
                    result,
                    "پرداخت با کیف پول با موفقیت انجام شد.");

                await _idempotencyService.CompleteAsync(
                    idempotencyKey,
                    JsonSerializer.Serialize(response),
                    StatusCodes.Status200OK);

                return Ok(response);
            }
            catch (Exception ex)
            {
                await _idempotencyService.FailAsync(idempotencyKey, ex.Message);
                throw;
            }
        }

        [AllowAnonymous]
        [HttpGet("zarinpal/callback")]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> ZarinpalCallback(
            [FromQuery(Name = "Authority")] string? authority1,
            [FromQuery(Name = "authority")] string? authority2,
            [FromQuery(Name = "Status")] string? status1,
            [FromQuery(Name = "status")] string? status2)
        {
            var authority = authority1 ?? authority2;
            var status = status1 ?? status2;

            if (string.IsNullOrWhiteSpace(authority))
                throw new BusinessException("Authority معتبر نیست.");

            if (string.IsNullOrWhiteSpace(status))
                status = "NOK";

            var result = await _paymentService.VerifyZarinpalPaymentAsync(
                authority,
                status);

            return Ok(ApiResult<PaymentVerifyResultDto>.Success(
                result,
                result.IsPaid
                    ? "پرداخت با موفقیت تایید شد."
                    : "پرداخت تایید نشد."));
        }

        [Authorize(Policy = "AdminOnly")]
        [HttpPost("zarinpal/reconcile")]
        public async Task<ActionResult<ApiResult<int>>> ReconcileZarinpal()
        {
            var count = await _paymentService.ReconcilePendingZarinpalPaymentsAsync();

            return Ok(ApiResult<int>.Success(
                count,
                "بررسی پرداخت‌های معلق زرین‌پال انجام شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}