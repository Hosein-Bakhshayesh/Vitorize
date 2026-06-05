using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/orders")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminOrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ICurrentUserService _currentUserService;

        public AdminOrdersController(
            IOrderService orderService,
            ICurrentUserService currentUserService)
        {
            _orderService = orderService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<OrderDto>>>> GetOrders()
        {
            var result = await _orderService.GetAdminOrdersAsync();

            return Ok(ApiResult<List<OrderDto>>.Success(
                result,
                "لیست سفارش‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("search")]
        public async Task<ActionResult<ApiResult<List<OrderDto>>>> SearchOrders(
            [FromQuery] AdminOrderFilterDto filter)
        {
            var result = await _orderService.SearchAdminOrdersAsync(filter);

            return Ok(ApiResult<List<OrderDto>>.Success(
                result,
                "جستجوی سفارش‌ها با موفقیت انجام شد."));
        }

        [HttpGet("{orderId:guid}")]
        public async Task<ActionResult<ApiResult<OrderDto>>> GetOrderDetails(
            Guid orderId)
        {
            var result = await _orderService.GetAdminOrderDetailsAsync(orderId);

            return Ok(ApiResult<OrderDto>.Success(
                result,
                "جزئیات سفارش با موفقیت دریافت شد."));
        }

        [HttpPost("{orderId:guid}/cancel")]
        public async Task<ActionResult<ApiResult>> CancelOrder(
            Guid orderId,
            CancelOrderRequestDto request)
        {
            var adminUserId = GetUserId();

            await _orderService.CancelOrderAsync(
                orderId,
                adminUserId,
                request.Reason);

            return Ok(ApiResult.Success("سفارش با موفقیت لغو شد."));
        }

        [HttpPost("{orderId:guid}/complete")]
        public async Task<ActionResult<ApiResult>> CompleteOrder(Guid orderId)
        {
            var adminUserId = GetUserId();

            await _orderService.CompleteOrderAsync(
                orderId,
                adminUserId);

            return Ok(ApiResult.Success("سفارش با موفقیت تکمیل شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}