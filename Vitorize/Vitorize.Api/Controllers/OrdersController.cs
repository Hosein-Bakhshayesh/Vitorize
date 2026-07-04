using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    [SwaggerTag("Customer order APIs for listing and viewing purchased orders.")]
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
        [SwaggerOperation(
            Summary = "لیست سفارش‌های من",
            Description = "دریافت لیست سفارش‌های کاربر لاگین‌شده.")]
        [ProducesResponseType(typeof(ApiResult<List<OrderDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<List<OrderDto>>>> GetMyOrders()
        {
            var userId = GetUserId();

            var result = await _orderService.GetMyOrdersAsync(userId);

            return Ok(ApiResult<List<OrderDto>>.Success(
                result,
                "لیست سفارش‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("deliveries")]
        [SwaggerOperation(
            Summary = "کتابخانه کدهای من",
            Description = "دریافت لیست یکجای تمام کدها و محتوای تحویل‌شده به کاربر لاگین‌شده از همه سفارش‌ها.")]
        [ProducesResponseType(typeof(ApiResult<List<DeliveredCodeDto>>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        public async Task<ActionResult<ApiResult<List<DeliveredCodeDto>>>> GetMyDeliveredCodes()
        {
            var userId = GetUserId();

            var result = await _orderService.GetMyDeliveredCodesAsync(userId);

            return Ok(ApiResult<List<DeliveredCodeDto>>.Success(
                result,
                "کدهای تحویل‌شده با موفقیت دریافت شدند."));
        }

        [HttpGet("{orderId:guid}")]
        [SwaggerOperation(
            Summary = "جزئیات سفارش من",
            Description = "دریافت جزئیات یک سفارش متعلق به کاربر لاگین‌شده، شامل آیتم‌ها و کدهای تحویل‌شده در صورت مجاز بودن.")]
        [ProducesResponseType(typeof(ApiResult<OrderDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(typeof(ApiResult), StatusCodes.Status404NotFound)]
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