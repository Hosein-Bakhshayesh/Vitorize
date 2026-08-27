using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services;

/// <summary>
/// Idempotent, non-financial orchestration after an order has committed as Paid.
/// Lifecycle creation and fulfillment stay in one serialized transaction; a
/// failure rolls back only this operational work, never the payment capture.
/// </summary>
public sealed class PostPaymentOrderProcessor : IPostPaymentOrderProcessor
{
    private readonly VitorizeDbContext _dbContext;
    private readonly IPaidGiftCodeAllocationService _paidGiftCodeAllocationService;
    private readonly IGiftCodeDeliveryService _giftCodeDeliveryService;
    private readonly INotificationService _notificationService;
    private readonly ISmsOutboxEnqueuer? _smsOutbox;
    private readonly ILogger<PostPaymentOrderProcessor> _logger;
    private readonly TimeProvider _timeProvider;

    public PostPaymentOrderProcessor(
        VitorizeDbContext dbContext,
        IPaidGiftCodeAllocationService paidGiftCodeAllocationService,
        IGiftCodeDeliveryService giftCodeDeliveryService,
        INotificationService notificationService,
        ILogger<PostPaymentOrderProcessor>? logger = null,
        ISmsOutboxEnqueuer? smsOutbox = null,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext;
        _paidGiftCodeAllocationService = paidGiftCodeAllocationService;
        _giftCodeDeliveryService = giftCodeDeliveryService;
        _notificationService = notificationService;
        _logger = logger ?? NullLogger<PostPaymentOrderProcessor>.Instance;
        _smsOutbox = smsOutbox;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task ProcessPaidOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        if (orderId == Guid.Empty)
            throw new BusinessException("شناسه سفارش معتبر نیست.");

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            await SqlServerTransactionLock.AcquireAsync(_dbContext, $"post-payment:order:{orderId:N}", cancellationToken);
            var order = await _dbContext.Orders
                .Include(x => x.User)
                .Include(x => x.OrderItems).ThenInclude(x => x.KycLifecycleState)
                .Include(x => x.OrderItems).ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.GiftCodeReservations).ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == orderId, cancellationToken)
                ?? throw new NotFoundException("سفارش یافت نشد.");

            if (order.PaymentStatus != (byte)PaymentStatus.Paid)
            {
                _logger.LogInformation(
                    "Post-payment processing ignored unpaid order. OrderId={OrderId}", orderId);
                await transaction.CommitAsync(cancellationToken);
                return;
            }

            var verificationSatisfied = KycVerificationSatisfaction.IsSatisfied(order.User);
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var item in order.OrderItems)
            {
                if (item.KycLifecycleState is not null)
                {
                    _logger.LogDebug(
                        "Post-payment KYC state already exists. OrderId={OrderId} OrderItemId={OrderItemId} Status={Status}",
                        order.Id, item.Id, item.KycLifecycleState.Status);
                    continue;
                }

                var initialStatus = OrderItemKycStateMachine.CreateInitialState(
                    item.RequiresVerification, verificationSatisfied);
                var state = new OrderItemKycState
                {
                    Id = Guid.NewGuid(),
                    OrderItemId = item.Id,
                    Status = (byte)initialStatus,
                    CreatedAt = now,
                    UpdatedAt = now,
                    SatisfiedAt = initialStatus == OrderItemKycStatus.Satisfied ? now : null,
                    SatisfiedByVerificationProfileId = null,
                    // PaidAt is the only authoritative start instant. Historical
                    // paid rows without it deliberately remain deadline-free.
                    CustomerActionDeadlineAt = initialStatus == OrderItemKycStatus.AwaitingSubmission && order.PaidAt.HasValue
                        ? KycCustomerActionDeadlineRules.CalculateInitialDeadline(order.PaidAt.Value, item.KycCustomerActionDeadlineHours)
                        : null
                };
                item.KycLifecycleState = state;
                await _dbContext.OrderItemKycStates.AddAsync(state, cancellationToken);
                _logger.LogInformation(
                    "Post-payment KYC state initialized. OrderId={OrderId} OrderItemId={OrderItemId} InitialStatus={InitialStatus} FulfillmentBlocked={FulfillmentBlocked}",
                    order.Id, item.Id, initialStatus, OrderItemKycStateMachine.BlocksFulfillment(initialStatus));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            // A Sold reservation/code is the durable paid allocation. This runs
            // after payment capture is committed, so allocation anomalies are
            // operationally retryable and cannot reverse the payment.
            foreach (var instantItem in order.OrderItems
                         .Where(x => x.DeliveryType == (byte)DeliveryType.Instant)
                         .OrderBy(x => x.ProductId).ThenBy(x => x.ProductVariantId).ThenBy(x => x.Id))
                await _paidGiftCodeAllocationService.EnsurePaidAllocationAsync(order, instantItem, cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);

            var hasUndeliveredEligibleInstantItem = order.OrderItems.Any(x =>
                x.DeliveryType == (byte)DeliveryType.Instant &&
                x.DeliveryStatus != (byte)DeliveryStatus.Delivered &&
                OrderItemFulfillmentEligibility.CanFulfill(x));
            if (hasUndeliveredEligibleInstantItem)
            {
                await _giftCodeDeliveryService.DeliverOrderAsync(order.Id, order.UserId);
                await _notificationService.CreateAsync(order.UserId, (byte)NotificationType.GiftCodeDelivered,
                    "تحویل سفارش", $"کدهای سفارش {order.OrderNumber} با موفقیت تحویل شدند.");
                if (_smsOutbox is not null)
                {
                    await _smsOutbox.EnqueueTextAsync(order.User.Mobile,
                        OrderSmsMessages.Completed(order.OrderNumber),
                        purpose: "OrderCompleted", aggregateId: order.Id, userId: order.UserId,
                        relatedEntityType: "Order", relatedEntityReference: order.OrderNumber,
                        cancellationToken: cancellationToken);
                }
            }

            await CreateSupportTicketIfRequiredAsync(order, now, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private async Task CreateSupportTicketIfRequiredAsync(Order order, DateTime now, CancellationToken cancellationToken)
    {
        var supportItems = order.OrderItems
            .Where(x => x.DeliveryType == (byte)DeliveryType.SupportRequired && OrderItemFulfillmentEligibility.CanFulfill(x))
            .ToList();
        if (supportItems.Count == 0)
            return;

        var productIds = supportItems.Select(x => x.ProductId).Distinct().ToList();
        var optInProductIds = await _dbContext.Products
            .Where(p => productIds.Contains(p.Id) && p.RequiresSupportMessage)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
        var qualifyingItems = supportItems.Where(x => optInProductIds.Contains(x.ProductId)).ToList();
        if (qualifyingItems.Count == 0)
            return;

        var existing = await _dbContext.Tickets
            .FirstOrDefaultAsync(t => t.OrderId == order.Id && t.IsFulfillmentTicket, cancellationToken);
        if (existing is not null)
        {
            foreach (var item in qualifyingItems.Where(x => x.SupportTicketId != existing.Id))
                item.SupportTicketId = existing.Id;
            return;
        }

        var ticketId = Guid.NewGuid();
        var ticket = new Ticket
        {
            Id = ticketId, UserId = order.UserId, OrderId = order.Id,
            Subject = $"پیگیری تحویل پشتیبانی - سفارش {order.OrderNumber}",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Status = (byte)TicketStatus.WaitingForAdmin, IsFulfillmentTicket = true,
            CreatedAt = now, UpdatedAt = now
        };
        ticket.TicketMessages.Add(new TicketMessage
        {
            Id = Guid.NewGuid(), TicketId = ticketId, SenderUserId = order.UserId,
            Message = BuildSupportFulfillmentMessage(order.OrderNumber, qualifyingItems),
            IsInternalNote = false, CreatedAt = now
        });
        await _dbContext.Tickets.AddAsync(ticket, cancellationToken);
        foreach (var item in qualifyingItems)
            item.SupportTicketId = ticketId;

        await _notificationService.CreateAsync(order.UserId, (byte)NotificationType.TicketCreated,
            "تیکت پشتیبانی ایجاد شد",
            $"برای سفارش {order.OrderNumber} یک تیکت پشتیبانی جهت تحویل محصول ایجاد شد.");
        _logger.LogInformation(
            "Support delivery ticket auto-created. OrderId={OrderId} TicketId={TicketId}", order.Id, ticketId);
    }

    private static string BuildSupportFulfillmentMessage(string orderNumber, IReadOnlyCollection<OrderItem> items)
    {
        var summary = string.Join("\n", items.Select(item =>
            $"• {item.ProductTitle} | نسخه: {(string.IsNullOrWhiteSpace(item.VariantTitle) ? "—" : item.VariantTitle)} | تعداد: {item.Quantity} | آیتم: {item.Id}"));
        return $"سفارش {orderNumber} با موفقیت ثبت شد و موارد زیر نیازمند آماده‌سازی پشتیبانی هستند:\n{summary}\nاطلاعات تحویل پس از آماده‌سازی از طریق همین تیکت ارسال خواهد شد.";
    }
}
