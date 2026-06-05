using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Interfaces;
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

        public PaymentsController(
            IPaymentService paymentService,
            ICurrentUserService currentUserService)
        {
            _paymentService = paymentService;
            _currentUserService = currentUserService;
        }

        [HttpPost("start/{orderId:guid}")]
        public async Task<ActionResult<ApiResult<PaymentStartResultDto>>> Start(
            Guid orderId)
        {
            var userId = GetUserId();

            var result = await _paymentService.StartPaymentAsync(
                userId,
                orderId);

            return Ok(ApiResult<PaymentStartResultDto>.Success(
                result,
                "پرداخت با موفقیت آماده شد."));
        }

        [HttpPost("mock/verify/{paymentId:guid}")]
        public async Task<ActionResult<ApiResult<PaymentVerifyResultDto>>> VerifyMock(
            Guid paymentId)
        {
            var userId = GetUserId();

            var result = await _paymentService.VerifyMockPaymentAsync(
                userId,
                paymentId);

            return Ok(ApiResult<PaymentVerifyResultDto>.Success(
                result,
                "پرداخت با موفقیت تایید شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}