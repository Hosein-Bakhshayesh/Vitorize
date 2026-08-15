using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Controllers.Admin;

[ApiController]
[Route("api/admin/kyc-finance/order-items")]
public sealed class AdminOrderItemKycFinanceController : ControllerBase
{
    private readonly IOrderItemKycFinanceResolutionService _service;
    private readonly ICurrentUserService _currentUser;
    public AdminOrderItemKycFinanceController(IOrderItemKycFinanceResolutionService service, ICurrentUserService currentUser) => (_service, _currentUser) = (service, currentUser);

    [HttpGet("{orderItemId:guid}")]
    [Authorize(Policy = "KycReview")]
    public async Task<ActionResult<ApiResult<OrderItemKycFinanceResolutionDto?>>> Get(Guid orderItemId, CancellationToken cancellationToken) =>
        Ok(ApiResult<OrderItemKycFinanceResolutionDto?>.Success(await _service.GetForOrderItemAsync(orderItemId, cancellationToken)));

    [HttpPost("{orderItemId:guid}/external-refund")]
    [Authorize(Policy = "FinanceManage")]
    public async Task<ActionResult<ApiResult<OrderItemKycFinanceResolutionDto>>> ResolveExternal(Guid orderItemId, ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResult<OrderItemKycFinanceResolutionDto>.Success(await _service.ResolveExternalAsync(orderItemId, RequireUserId(), request, cancellationToken), "بازپرداخت خارجی ثبت شد."));

    [HttpPost("{orderItemId:guid}/no-refund")]
    [Authorize(Policy = "FinanceManage")]
    public async Task<ActionResult<ApiResult<OrderItemKycFinanceResolutionDto>>> ResolveNoRefund(Guid orderItemId, ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken) =>
        Ok(ApiResult<OrderItemKycFinanceResolutionDto>.Success(await _service.ResolveNoRefundAsync(orderItemId, RequireUserId(), request, cancellationToken), "تصمیم مالی ثبت شد."));

    private Guid RequireUserId() => _currentUser.UserId ?? throw new UnauthorizedException("ادمین احراز هویت نشده است.");
}
