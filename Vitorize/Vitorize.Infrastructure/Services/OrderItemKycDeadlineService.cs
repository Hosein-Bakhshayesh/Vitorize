using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// The single authority for deadline state mutation. The worker only calls the
/// idempotent convergence methods; customer commands remain the security boundary.
/// </summary>
public sealed class OrderItemKycDeadlineService : IOrderItemKycDeadlineService
{
    private readonly VitorizeDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<OrderItemKycDeadlineService> _logger;
    private readonly IOrderItemKycFinanceResolutionService _financeResolutions;

    public OrderItemKycDeadlineService(VitorizeDbContext db, TimeProvider? timeProvider = null,
        ILogger<OrderItemKycDeadlineService>? logger = null,
        IOrderItemKycFinanceResolutionService? financeResolutions = null)
    {
        _db = db;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _logger = logger ?? NullLogger<OrderItemKycDeadlineService>.Instance;
        _financeResolutions = financeResolutions ?? new OrderItemKycFinanceResolutionService(db);
    }

    public async Task<CustomerDeadlineEnforcementResult> EnforceCustomerActionsWithinTransactionAsync(
        Guid userId, Guid? orderItemId = null, CancellationToken cancellationToken = default)
    {
        if (_db.Database.CurrentTransaction is null)
            throw new InvalidOperationException("Customer deadline enforcement requires the verification transaction.");

        var now = UtcNow();
        var states = await _db.OrderItemKycStates
            .Include(x => x.OrderItem)
            .Where(x => x.OrderItem.Order.UserId == userId && x.OrderItem.Order.PaymentStatus == (byte)PaymentStatus.Paid &&
                        (!orderItemId.HasValue || x.OrderItemId == orderItemId.Value))
            .ToListAsync(cancellationToken);
        var expired = 0;
        var eligible = 0;
        foreach (var state in states)
        {
            var status = (OrderItemKycStatus)state.Status;
            if (!KycCustomerActionDeadlineRules.AppliesTo(status))
                continue;
            if (KycCustomerActionDeadlineRules.IsOverdue(status, state.CustomerActionDeadlineAt, now))
            {
                ApplyExpiry(state, now, null);
                expired++;
            }
            else
            {
                eligible++;
            }
        }
        return new CustomerDeadlineEnforcementResult(expired, eligible);
    }

    public async Task<bool> ExpireIfOverdueAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        var ownerId = await _db.OrderItems.AsNoTracking().Where(x => x.Id == orderItemId)
            .Select(x => (Guid?)x.Order.UserId).SingleOrDefaultAsync(cancellationToken);
        if (!ownerId.HasValue) return false;

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await SqlServerTransactionLock.AcquireAsync(_db, $"verification:user:{ownerId.Value:N}", cancellationToken);
            var state = await _db.OrderItemKycStates.Include(x => x.OrderItem).ThenInclude(x => x.Order)
                .SingleOrDefaultAsync(x => x.OrderItemId == orderItemId, cancellationToken);
            if (state is null)
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
            var now = UtcNow();
            var status = (OrderItemKycStatus)state.Status;
            if (!KycCustomerActionDeadlineRules.IsOverdue(status, state.CustomerActionDeadlineAt, now))
            {
                await transaction.CommitAsync(cancellationToken);
                return false;
            }
            ApplyExpiry(state, now, null);
            await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return true;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<int> ProcessOverdueBatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0) throw new ArgumentOutOfRangeException(nameof(batchSize));
        var now = UtcNow();
        var candidateIds = await _db.OrderItemKycStates.AsNoTracking()
            .Where(x => (x.Status == (byte)OrderItemKycStatus.AwaitingSubmission || x.Status == (byte)OrderItemKycStatus.Rejected) &&
                        x.CustomerActionDeadlineAt.HasValue && x.CustomerActionDeadlineAt.Value <= now)
            .OrderBy(x => x.CustomerActionDeadlineAt).ThenBy(x => x.Id)
            .Select(x => x.OrderItemId).Take(batchSize).ToListAsync(cancellationToken);
        var changed = 0;
        foreach (var id in candidateIds)
        {
            try
            {
                if (await ExpireIfOverdueAsync(id, cancellationToken)) changed++;
            }
            catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
            {
                // Candidate failures are isolated so a corrupt row cannot halt convergence.
                _logger.LogError(exception, "KYC deadline candidate processing failed. OrderItemId={OrderItemId} ExceptionType={ExceptionType}",
                    id, exception.GetType().Name);
            }
        }
        return changed;
    }

    public Task<OrderItemKycDeadlineOperationDto> ExtendDeadlineAsync(Guid orderItemId, DateTime newDeadlineAtUtc,
        Guid adminUserId, CancellationToken cancellationToken = default) =>
        ExecuteAdminOperationAsync(orderItemId, newDeadlineAtUtc, adminUserId, AdminOperation.Extend, cancellationToken);

    public Task<OrderItemKycDeadlineOperationDto> ReopenExpiredAsync(Guid orderItemId, DateTime newDeadlineAtUtc,
        Guid adminUserId, CancellationToken cancellationToken = default) =>
        ExecuteAdminOperationAsync(orderItemId, newDeadlineAtUtc, adminUserId, AdminOperation.Reopen, cancellationToken);

    public Task<OrderItemKycDeadlineOperationDto> FinalRejectExpiredAsync(Guid orderItemId, Guid adminUserId,
        CancellationToken cancellationToken = default) =>
        ExecuteAdminOperationAsync(orderItemId, null, adminUserId, AdminOperation.FinalReject, cancellationToken);

    private async Task<OrderItemKycDeadlineOperationDto> ExecuteAdminOperationAsync(Guid orderItemId, DateTime? requestedDeadline,
        Guid adminUserId, AdminOperation operation, CancellationToken cancellationToken)
    {
        if (adminUserId == Guid.Empty) throw new UnauthorizedException("مدیر احراز هویت نشده است.");
        var ownerId = await _db.OrderItems.AsNoTracking().Where(x => x.Id == orderItemId)
            .Select(x => (Guid?)x.Order.UserId).SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("آیتم سفارش یافت نشد.");
        var utcDeadline = requestedDeadline.HasValue ? RequireFutureUtc(requestedDeadline.Value) : (DateTime?)null;

        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await SqlServerTransactionLock.AcquireAsync(_db, $"verification:user:{ownerId:N}", cancellationToken);
            var state = await _db.OrderItemKycStates.Include(x => x.OrderItem).ThenInclude(x => x.Order)
                .SingleOrDefaultAsync(x => x.OrderItemId == orderItemId, cancellationToken)
                ?? throw new NotFoundException("چرخه احراز هویت آیتم یافت نشد.");
            var now = UtcNow();
            var status = (OrderItemKycStatus)state.Status;
            var previousDeadline = state.CustomerActionDeadlineAt;
            var changed = false;

            if (operation == AdminOperation.Extend)
            {
                if (!KycCustomerActionDeadlineRules.AppliesTo(status) || !state.OrderItem.KycCustomerActionDeadlineHours.HasValue || !previousDeadline.HasValue)
                    throw new BusinessException("این آیتم مهلت اقدام قابل تمدید ندارد.");
                if (KycCustomerActionDeadlineRules.IsOverdue(status, previousDeadline, now))
                {
                    ApplyExpiry(state, now, adminUserId);
                    await _db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    throw new ConcurrencyConflictException("مهلت اقدام پایان یافته است؛ برای ادامه آیتم را بازگشایی کنید.");
                }
                if (utcDeadline!.Value < previousDeadline.Value)
                    throw new BusinessException("مهلت جدید باید بعد از مهلت فعلی باشد.");
                if (utcDeadline.Value > previousDeadline.Value)
                {
                    state.CustomerActionDeadlineAt = utcDeadline;
                    state.UpdatedAt = now;
                    AddAudit(state, "KycDeadlineExtended", status, previousDeadline, utcDeadline, adminUserId, now);
                    changed = true;
                }
            }
            else if (operation == AdminOperation.Reopen)
            {
                if (status != OrderItemKycStatus.Expired || !state.OrderItem.KycCustomerActionDeadlineHours.HasValue)
                    throw new BusinessException("فقط آیتم منقضی‌شده دارای مهلت قابل بازگشایی است.");
                OrderItemKycStateMachine.EnsureTransition(status, OrderItemKycStatus.AwaitingSubmission);
                state.Status = (byte)OrderItemKycStatus.AwaitingSubmission;
                state.CustomerActionDeadlineAt = utcDeadline;
                state.UpdatedAt = now;
                AddAudit(state, "KycDeadlineReopened", status, previousDeadline, utcDeadline, adminUserId, now);
                changed = true;
            }
            else
            {
                if (status != OrderItemKycStatus.Expired)
                    throw new BusinessException("فقط آیتم منقضی‌شده قابل رد نهایی است.");
                OrderItemKycStateMachine.EnsureTransition(status, OrderItemKycStatus.FinalRejected);
                state.Status = (byte)OrderItemKycStatus.FinalRejected;
                state.CustomerActionDeadlineAt = null;
                state.UpdatedAt = now;
                AddAudit(state, "KycDeadlineFinalRejected", status, previousDeadline, null, adminUserId, now);
                await _financeResolutions.EnsurePendingAsync(orderItemId, cancellationToken);
                changed = true;
            }

            if (changed) await _db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new OrderItemKycDeadlineOperationDto { OrderItemId = orderItemId, LifecycleStatus = state.Status,
                CustomerActionDeadlineAt = state.CustomerActionDeadlineAt, Changed = changed };
        }
        catch
        {
            if (_db.Database.CurrentTransaction is not null) await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private void ApplyExpiry(OrderItemKycState state, DateTime now, Guid? actorId)
    {
        var current = (OrderItemKycStatus)state.Status;
        OrderItemKycStateMachine.EnsureTransition(current, OrderItemKycStatus.Expired);
        var previousDeadline = state.CustomerActionDeadlineAt;
        state.Status = (byte)OrderItemKycStatus.Expired;
        state.CustomerActionDeadlineAt = null;
        state.UpdatedAt = now;
        AddAudit(state, "KycDeadlineExpired", current, previousDeadline, null, actorId, now);
    }

    private void AddAudit(OrderItemKycState state, string action, OrderItemKycStatus previousStatus, DateTime? previousDeadline, DateTime? newDeadline,
        Guid? actorId, DateTime now) => _db.AuditLogs.Add(new AuditLog
    {
        Id = Guid.NewGuid(), UserId = actorId, ActionType = action, EntityName = nameof(OrderItemKycState),
        EntityId = state.Id.ToString(), CreatedAt = now,
        Data = JsonSerializer.Serialize(new { state.OrderItemId, PreviousStatus = (byte)previousStatus, NewStatus = state.Status,
            PreviousDeadlineAt = previousDeadline, NewDeadlineAt = newDeadline })
    });

    private DateTime UtcNow() => _timeProvider.GetUtcNow().UtcDateTime;

    private DateTime RequireFutureUtc(DateTime value)
    {
        if (value.Kind != DateTimeKind.Utc || value <= UtcNow())
            throw new BusinessException("مهلت جدید باید به‌صورت زمان UTC و در آینده باشد.");
        return value;
    }

    private enum AdminOperation { Extend, Reopen, FinalReject }
}
