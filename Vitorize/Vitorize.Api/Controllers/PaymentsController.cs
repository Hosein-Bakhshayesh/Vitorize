using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
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
    [SwaggerTag("Payment APIs for Zarinpal, mock payment, wallet payment and payment reconciliation.")]
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
        [SwaggerOperation(
    Summary = "شروع پرداخت زرین‌پال",
    Description = "ایجاد درخواست پرداخت برای سفارش و دریافت PaymentUrl جهت انتقال کاربر به درگاه زرین‌پال.")]
        [ProducesResponseType(typeof(ApiResult<PaymentStartResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResult<PaymentStartResultDto>>> Start(Guid orderId)
        {
            var userId = GetUserId();

            var result = await _paymentService.StartPaymentAsync(userId, orderId);

            return Ok(ApiResult<PaymentStartResultDto>.Success(
                result,
                "پرداخت با موفقیت آماده شد."));
        }

        [HttpPost("mock/verify/{paymentId:guid}")]
        [SwaggerOperation(
            Summary = "تایید پرداخت تستی",
            Description = "تایید Mock Payment برای تست داخلی بدون اتصال به درگاه واقعی.")]
        [ProducesResponseType(typeof(ApiResult<PaymentVerifyResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> VerifyMock(Guid paymentId)
        {
            var userId = GetUserId();

            var result = await _paymentService.VerifyMockPaymentAsync(userId, paymentId);

            return Ok(ApiResult<PaymentVerifyResultDto>.Success(
                result,
                "پرداخت با موفقیت تایید شد."));
        }

        [HttpPost("wallet/pay/{orderId:guid}")]
        [SwaggerOperation(
    Summary = "پرداخت با کیف پول",
    Description = "پرداخت سفارش از موجودی کیف پول کاربر. ارسال Header با نام Idempotency-Key الزامی است.")]
        [ProducesResponseType(typeof(ApiResult<PaymentVerifyResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> PayWithWallet(Guid orderId,
    [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(idempotencyKey))
                throw new BusinessException("Idempotency-Key الزامی است.");

            var requestHash = RequestHashHelper.ComputeHash(new
            {
                UserId = userId,
                OrderId = orderId,
                Action = "WalletPay"
            });

            await _idempotencyService.StartAsync(
                userId,
                idempotencyKey,
                requestHash);

            try
            {
                var result = await _paymentService.PayWithWalletAsync(
                    userId,
                    orderId);

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
                await _idempotencyService.FailAsync(
                    idempotencyKey,
                    ex.Message);

                throw;
            }
        }

        [AllowAnonymous]
        [HttpGet("zarinpal/callback")]
        [SwaggerOperation(
            Summary = "Callback زرین‌پال",
            Description = "آدرس برگشت زرین‌پال پس از پرداخت. این Endpoint عمومی است و پس از دریافت Authority و Status، Verify سمت سرور انجام می‌دهد.")]
        [ProducesResponseType(typeof(ApiResult<PaymentVerifyResultDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
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
        [SwaggerOperation(
            Summary = "بررسی پرداخت‌های معلق زرین‌پال",
            Description = "بررسی مجدد پرداخت‌های Pending قدیمی زرین‌پال و تلاش برای تایید یا Fail کردن آن‌ها. فقط Admin.")]
        [ProducesResponseType(typeof(ApiResult<int>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status403Forbidden)]
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