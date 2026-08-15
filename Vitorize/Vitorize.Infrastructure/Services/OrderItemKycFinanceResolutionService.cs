using System.Data;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Records the explicit financial decision for a terminally rejected KYC item.
/// PaymentRefund remains deliberately order-level; this service never guesses a
/// partial amount or changes payment/inventory state.
/// </summary>
public sealed class OrderItemKycFinanceResolutionService : IOrderItemKycFinanceResolutionService
{
    private readonly VitorizeDbContext _db;
    public OrderItemKycFinanceResolutionService(VitorizeDbContext db) => _db = db;

    public async Task EnsurePendingAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        if (await _db.OrderItemKycFinanceResolutions.AnyAsync(x => x.OrderItemId == orderItemId, cancellationToken)) return;
        await _db.OrderItemKycFinanceResolutions.AddAsync(new OrderItemKycFinanceResolution
        {
            Id = Guid.NewGuid(), OrderItemId = orderItemId,
            Status = (byte)OrderItemKycFinanceResolutionStatus.Pending, CreatedAt = DateTime.UtcNow
        }, cancellationToken);
    }

    public async Task<OrderItemKycFinanceResolutionDto?> GetForOrderItemAsync(Guid orderItemId, CancellationToken cancellationToken = default) =>
        await _db.OrderItemKycFinanceResolutions.AsNoTracking().Where(x => x.OrderItemId == orderItemId)
            .Select(x => Map(x)).SingleOrDefaultAsync(cancellationToken);

    public Task<OrderItemKycFinanceResolutionDto> ResolveExternalAsync(Guid orderItemId, Guid actorUserId,
        ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken = default) =>
        ResolveAsync(orderItemId, actorUserId, request, OrderItemKycFinanceResolutionStatus.ResolvedExternalRefund, cancellationToken);

    public Task<OrderItemKycFinanceResolutionDto> ResolveNoRefundAsync(Guid orderItemId, Guid actorUserId,
        ResolveOrderItemKycFinanceRequestDto request, CancellationToken cancellationToken = default) =>
        ResolveAsync(orderItemId, actorUserId, request, OrderItemKycFinanceResolutionStatus.ResolvedNoRefund, cancellationToken);

    private async Task<OrderItemKycFinanceResolutionDto> ResolveAsync(Guid orderItemId, Guid actorUserId,
        ResolveOrderItemKycFinanceRequestDto request, OrderItemKycFinanceResolutionStatus target, CancellationToken cancellationToken)
    {
        if (actorUserId == Guid.Empty) throw new UnauthorizedException("کاربر مالی احراز هویت نشده است.");
        request ??= new ResolveOrderItemKycFinanceRequestDto();
        var reason = request.Reason?.Trim();
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 1000) throw new BusinessException("دلیل تصمیم مالی الزامی است.");
        var reference = request.ExternalReference?.Trim();
        if (target == OrderItemKycFinanceResolutionStatus.ResolvedExternalRefund && string.IsNullOrWhiteSpace(reference))
            throw new BusinessException("شماره پیگیری بازپرداخت خارجی الزامی است.");

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await SqlServerTransactionLock.AcquireAsync(_db, $"kyc-finance:item:{orderItemId:N}", cancellationToken);
            var item = await _db.OrderItems.Include(x => x.Order).Include(x => x.KycLifecycleState)
                .SingleOrDefaultAsync(x => x.Id == orderItemId, cancellationToken) ?? throw new NotFoundException("آیتم سفارش یافت نشد.");
            if (item.Order.PaymentStatus != (byte)PaymentStatus.Paid || item.KycLifecycleState?.Status != (byte)OrderItemKycStatus.FinalRejected)
                throw new BusinessException("این آیتم برای تعیین تکلیف مالی قابل اقدام نیست.");
            var resolution = await _db.OrderItemKycFinanceResolutions.SingleOrDefaultAsync(x => x.OrderItemId == orderItemId, cancellationToken);
            if (resolution is null)
            {
                resolution = new OrderItemKycFinanceResolution { Id = Guid.NewGuid(), OrderItemId = orderItemId, Status = (byte)OrderItemKycFinanceResolutionStatus.Pending, CreatedAt = DateTime.UtcNow };
                await _db.OrderItemKycFinanceResolutions.AddAsync(resolution, cancellationToken);
            }
            if (resolution.Status != (byte)OrderItemKycFinanceResolutionStatus.Pending)
                throw new ConcurrencyConflictException("این آیتم پیش‌تر تعیین تکلیف مالی شده است.");

            var now = DateTime.UtcNow;
            resolution.Status = (byte)target;
            resolution.Reason = reason;
            resolution.ExternalReference = target == OrderItemKycFinanceResolutionStatus.ResolvedExternalRefund ? reference : null;
            resolution.ResolvedByUserId = actorUserId;
            resolution.ResolvedAt = now;
            await _db.FinancialAuditLogs.AddAsync(new FinancialAuditLog
            {
                EventType = target == OrderItemKycFinanceResolutionStatus.ResolvedExternalRefund ? "KycFinanceExternalRefundRecorded" : "KycFinanceNoRefundRecorded",
                EntityType = nameof(OrderItemKycFinanceResolution), EntityId = resolution.Id, UserId = actorUserId,
                CorrelationId = item.OrderId, Detail = $"orderItem:{orderItemId:N};reference:{resolution.ExternalReference ?? "none"};reason:{reason}", CreatedAt = now
            }, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Map(resolution);
        }
        catch { await transaction.RollbackAsync(cancellationToken); throw; }
    }

    private static OrderItemKycFinanceResolutionDto Map(OrderItemKycFinanceResolution value) => new()
    { OrderItemId = value.OrderItemId, Status = value.Status, Reason = value.Reason, ExternalReference = value.ExternalReference, CreatedAt = value.CreatedAt, ResolvedAt = value.ResolvedAt };
}
