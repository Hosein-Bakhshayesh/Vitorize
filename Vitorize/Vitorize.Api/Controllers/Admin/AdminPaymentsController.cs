using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Payments;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/payments")]
    public class AdminPaymentsController : ControllerBase
    {
        private readonly IAdminPaymentReadService _service;
        private readonly IPaymentService _paymentService;
        private readonly ICurrentUserService _currentUser;
        public AdminPaymentsController(IAdminPaymentReadService service, IPaymentService paymentService, ICurrentUserService currentUser)
        {
            _service = service;
            _paymentService = paymentService;
            _currentUser = currentUser;
        }
        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminPaymentDto>>>> GetAll([FromQuery] AdminQueryFilterDto filter)
        {
            var result = await _service.GetAllAsync(filter);
            return Ok(ApiResult<List<AdminPaymentDto>>.Success(result, "پرداخت‌ها با موفقیت دریافت شدند."));
        }
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminPaymentDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return Ok(ApiResult<AdminPaymentDto>.Success(result, "جزئیات پرداخت با موفقیت دریافت شد."));
        }

        [HttpPost("{id:guid}/refund")]
        [Authorize(Policy = "FinanceManage")]
        public async Task<ActionResult<ApiResult<PaymentRefundDto>>> Refund(Guid id, PaymentRefundRequestDto request)
        {
            var result = await _paymentService.RefundAsync(id, RequireUserId(), request);
            return Ok(ApiResult<PaymentRefundDto>.Success(result, "درخواست بازپرداخت ثبت شد."));
        }

        [HttpPost("refunds/{refundId:guid}/complete")]
        [Authorize(Policy = "FinanceManage")]
        public async Task<ActionResult<ApiResult<PaymentRefundDto>>> CompleteRefund(
            Guid refundId, CompletePaymentRefundRequestDto request)
        {
            var result = await _paymentService.CompleteRefundAsync(refundId, RequireUserId(), request.GatewayReference);
            return Ok(ApiResult<PaymentRefundDto>.Success(result, "بازپرداخت درگاه تکمیل شد."));
        }

        private Guid RequireUserId() => _currentUser.UserId
            ?? throw new Vitorize.Shared.Exceptions.UnauthorizedException("ادمین احراز هویت نشده است.");
    }
}
