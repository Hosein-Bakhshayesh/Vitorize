using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Post-commit recovery-safe release for the small set of items satisfied by a
/// verification profile. Each item is isolated so one corrupt allocation does
/// not prevent a different support/manual item from progressing.
/// </summary>
public sealed class OrderItemFulfillmentReleaseService : IOrderItemFulfillmentReleaseService
{
    private readonly VitorizeDbContext _dbContext;
    private readonly IGiftCodeDeliveryService _giftCodeDeliveryService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<OrderItemFulfillmentReleaseService> _logger;

    public OrderItemFulfillmentReleaseService(
        VitorizeDbContext dbContext,
        IGiftCodeDeliveryService giftCodeDeliveryService,
        INotificationService notificationService,
        ILogger<OrderItemFulfillmentReleaseService>? logger = null)
    {
        _dbContext = dbContext;
        _giftCodeDeliveryService = giftCodeDeliveryService;
        _notificationService = notificationService;
        _logger = logger ?? NullLogger<OrderItemFulfillmentReleaseService>.Instance;
    }

    public async Task ReleaseSatisfiedItemsForVerificationAsync(
        Guid verificationProfileId,
        CancellationToken cancellationToken = default)
    {
        if (verificationProfileId == Guid.Empty)
            return;

        var itemIds = await _dbContext.OrderItemKycStates.AsNoTracking()
            .Where(x => x.SatisfiedByVerificationProfileId == verificationProfileId &&
                        x.Status == (byte)OrderItemKycStatus.Satisfied &&
                        x.OrderItem.Order.PaymentStatus == (byte)PaymentStatus.Paid)
            .Select(x => x.OrderItemId)
            .ToListAsync(cancellationToken);

        foreach (var itemId in itemIds)
        {
            try
            {
                await ReleaseSatisfiedOrderItemAsync(itemId, cancellationToken);
            }
            catch (Exception exception)
            {
                // Approval is already durable. The public item-level entry
                // point deliberately permits operational reconciliation later.
                _logger.LogError(exception,
                    "KYC fulfillment release failed and remains retryable. OrderItemId={OrderItemId}", itemId);
            }
        }
    }

    public async Task ReleaseSatisfiedOrderItemAsync(Guid orderItemId, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.OrderItems.AsNoTracking()
            .Include(x => x.Order)
            .Include(x => x.KycLifecycleState)
            .FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken)
            ?? throw new NotFoundException("آیتم سفارش یافت نشد.");

        if (item.Order.PaymentStatus != (byte)PaymentStatus.Paid ||
            item.KycLifecycleState?.Status != (byte)OrderItemKycStatus.Satisfied)
        {
            _logger.LogInformation(
                "KYC fulfillment release skipped. OrderId={OrderId} OrderItemId={OrderItemId} KycStatus={KycStatus}",
                item.OrderId, item.Id, item.KycLifecycleState?.Status);
            return;
        }

        switch ((DeliveryType)item.DeliveryType)
        {
            case DeliveryType.Instant:
                var released = await _giftCodeDeliveryService.DeliverSatisfiedOrderItemAsync(
                    item.Id, item.Order.UserId, cancellationToken);
                if (released)
                    await _notificationService.CreateAsync(item.Order.UserId, (byte)NotificationType.GiftCodeDelivered,
                        "تحویل سفارش", $"کدهای آیتم سفارش {item.Order.OrderNumber} با موفقیت تحویل شدند.");
                break;
            case DeliveryType.SupportRequired:
                await EnsureSupportWorkAsync(item.Id, cancellationToken);
                break;
            case DeliveryType.Manual:
                _logger.LogInformation(
                    "KYC-satisfied manual item is now eligible for normal manual delivery. OrderId={OrderId} OrderItemId={OrderItemId}",
                    item.OrderId, item.Id);
                break;
            default:
                throw new BusinessException("نوع تحویل آیتم سفارش معتبر نیست.");
        }
    }

    private async Task EnsureSupportWorkAsync(Guid orderItemId, CancellationToken cancellationToken)
    {
        // Resolve the deterministic lock key before opening the serializable
        // transaction. Acquiring it first prevents concurrent readers from
        // holding range locks while waiting on this aggregate lock.
        var orderId = await _dbContext.OrderItems.AsNoTracking()
            .Where(x => x.Id == orderItemId)
            .Select(x => x.OrderId)
            .FirstOrDefaultAsync(cancellationToken);
        if (orderId == Guid.Empty)
            throw new NotFoundException("آیتم سفارش یافت نشد.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        try
        {
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"kyc-release:order:{orderId:N}", cancellationToken);
            var item = await _dbContext.OrderItems
                .Include(x => x.Order).ThenInclude(x => x.OrderItems).ThenInclude(x => x.KycLifecycleState)
                .FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken)
                ?? throw new NotFoundException("آیتم سفارش یافت نشد.");

            if (item.Order.PaymentStatus != (byte)PaymentStatus.Paid ||
                item.DeliveryType != (byte)DeliveryType.SupportRequired ||
                item.KycLifecycleState?.Status != (byte)OrderItemKycStatus.Satisfied)
            {
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var qualifyingItems = item.Order.OrderItems
                .Where(x => x.DeliveryType == (byte)DeliveryType.SupportRequired &&
                            x.KycLifecycleState?.Status == (byte)OrderItemKycStatus.Satisfied)
                .ToList();
            var existing = await _dbContext.Tickets
                .FirstOrDefaultAsync(x => x.OrderId == item.OrderId && x.IsFulfillmentTicket, cancellationToken);
            if (existing is not null)
            {
                foreach (var qualifyingItem in qualifyingItems.Where(x => x.SupportTicketId != existing.Id))
                    qualifyingItem.SupportTicketId = existing.Id;
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var now = DateTime.UtcNow;
            var ticketId = Guid.NewGuid();
            var ticket = new Ticket
            {
                Id = ticketId, UserId = item.Order.UserId, OrderId = item.OrderId,
                Subject = $"پیگیری تحویل پشتیبانی - سفارش {item.Order.OrderNumber}",
                Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
                Status = (byte)TicketStatus.WaitingForAdmin, IsFulfillmentTicket = true,
                CreatedAt = now, UpdatedAt = now
            };
            ticket.TicketMessages.Add(new TicketMessage
            {
                Id = Guid.NewGuid(), TicketId = ticketId, SenderUserId = item.Order.UserId,
                Message = $"آیتم‌های تاییدشده سفارش {item.Order.OrderNumber} نیازمند آماده‌سازی پشتیبانی هستند.",
                IsInternalNote = false, CreatedAt = now
            });
            await _dbContext.Tickets.AddAsync(ticket, cancellationToken);
            foreach (var qualifyingItem in qualifyingItems)
                qualifyingItem.SupportTicketId = ticketId;
            await _notificationService.CreateAsync(item.Order.UserId, (byte)NotificationType.TicketCreated,
                "تیکت پشتیبانی ایجاد شد", $"برای سفارش {item.Order.OrderNumber} یک تیکت پشتیبانی جهت تحویل محصول ایجاد شد.");
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation(
                "KYC-satisfied support item released. OrderId={OrderId} OrderItemId={OrderItemId} TicketId={TicketId}",
                item.OrderId, item.Id, ticketId);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
