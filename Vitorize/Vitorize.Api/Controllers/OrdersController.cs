using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class OrdersController : ControllerBase
    {
        private readonly IOrderService _orderService;
        private readonly ICurrentUserService _currentUserService;

        public OrdersController(
            IOrderService orderService,
            ICurrentUserService currentUserService)
        {
            _orderService = orderService;
            _currentUserService = currentUserService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<OrderDto>>>> GetMyOrders()
        {
            var userId = GetUserId();

            var result = await _orderService.GetMyOrdersAsync(userId);

            return Ok(ApiResult<List<OrderDto>>.Success(
                result,
                "لیست سفارش‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("{orderId:guid}")]
        public async Task<ActionResult<ApiResult<OrderDto>>> GetMyOrderDetails(
            Guid orderId)
        {
            var userId = GetUserId();

            var result = await _orderService.GetMyOrderDetailsAsync(
                userId,
                orderId);

            return Ok(ApiResult<OrderDto>.Success(
                result,
                "جزئیات سفارش با موفقیت دریافت شد."));
        }

        private Guid GetUserId()
        {
            if (!_currentUserService.UserId.HasValue)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return _currentUserService.UserId.Value;
        }
    }
}