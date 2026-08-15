using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
using System.Data;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class GiftCodeDeliveryService : IGiftCodeDeliveryService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<GiftCodeDeliveryService> _logger;

        public GiftCodeDeliveryService(
            VitorizeDbContext dbContext,
            IEncryptionService encryptionService,
            ILogger<GiftCodeDeliveryService>? logger = null)
        {
            _dbContext = dbContext;
            _encryptionService = encryptionService;
            _logger = logger ?? NullLogger<GiftCodeDeliveryService>.Instance;
        }

        public async Task DeliverOrderAsync(
            Guid orderId,
            Guid? deliveredByUserId = null)
        {
            var stopwatch = Stopwatch.StartNew();
            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.KycLifecycleState)
                .Include(x => x.GiftCodeReservations)
                    .ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.PaymentStatus != (byte)PaymentStatus.Paid)
                throw new BusinessException("سفارش هنوز پرداخت نشده است.");

            var now = DateTime.UtcNow;

            var soldReservations = order.GiftCodeReservations
                .Where(x =>
                    x.Status == (byte)GiftCodeReservationStatus.Sold &&
                    x.OrderItemId.HasValue &&
                    x.GiftCode.Status == (byte)GiftCodeStatus.Sold)
                .ToList();

            // سفارش‌های تحویل دستی رزرو کد ندارند؛ فقط وقتی آیتم تحویل آنی وجود دارد نبودِ کد خطاست.
            var hasEligibleInstantItems = order.OrderItems
                .Any(x => x.DeliveryType == (byte)DeliveryType.Instant && OrderItemFulfillmentEligibility.CanFulfill(x));

            if (hasEligibleInstantItems && !soldReservations.Any())
            {
                var instantItemsAlreadyDelivered = order.OrderItems
                    .Where(x => x.DeliveryType == (byte)DeliveryType.Instant)
                    .All(x => x.DeliveryStatus == (byte)DeliveryStatus.Delivered &&
                              x.OrderItemDeliveries.Any());
                if (instantItemsAlreadyDelivered)
                {
                    _logger.LogInformation(
                        "Gift-code delivery replay ignored for completed fulfillment. OrderNumber={OrderNumber} EventType={EventType}",
                        order.OrderNumber, "GiftCodeDeliveryReplayIgnored");
                    return;
                }

                throw new BusinessException("کد قابل تحویلی برای این سفارش یافت نشد.");
            }

            foreach (var reservation in soldReservations)
            {
                var orderItem = order.OrderItems
                    .FirstOrDefault(x => x.Id == reservation.OrderItemId.GetValueOrDefault());

                if (orderItem == null)
                    throw new BusinessException("آیتم سفارش برای کد رزرو شده یافت نشد.");

                if (!OrderItemFulfillmentEligibility.CanFulfill(orderItem))
                {
                    _logger.LogInformation(
                        "Gift-code fulfillment blocked by item KYC lifecycle. OrderId={OrderId} OrderItemId={OrderItemId}",
                        order.Id, orderItem.Id);
                    continue;
                }

                var alreadyDelivered = orderItem.OrderItemDeliveries
                    .Any(x => x.GiftCodeId == reservation.GiftCodeId);

                if (alreadyDelivered)
                    continue;

                var giftCode = reservation.GiftCode;

                var decryptedCode = _encryptionService.Decrypt(
                    giftCode.EncryptedCode);

                var delivery = new OrderItemDelivery
                {
                    Id = Guid.NewGuid(),
                    OrderItemId = orderItem.Id,
                    DeliveryType = (byte)DeliveryType.Instant,
                    GiftCodeId = giftCode.Id,
                    DeliveredContent = _encryptionService.Encrypt(decryptedCode),
                    ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(decryptedCode))),
                    EncryptionVersion = 2,
                    IsVisibleToCustomer = true,
                    DeliveredByUserId = deliveredByUserId,
                    CreatedAt = now
                };

                await _dbContext.OrderItemDeliveries.AddAsync(delivery);

                giftCode.Status = (byte)GiftCodeStatus.Delivered;
                giftCode.DeliveredAt = now;
                giftCode.UpdatedAt = now;

                orderItem.DeliveryStatus = (byte)DeliveryStatus.Delivered;
                orderItem.DeliveredAt = now;

                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = "GiftCodeDelivered",
                    EntityType = "OrderItemDelivery",
                    EntityId = delivery.Id,
                    UserId = deliveredByUserId,
                    CorrelationId = order.Id,
                    Detail = $"order:{order.OrderNumber}",
                    CreatedAt = now
                });
            }

            if (OrderFulfillmentRules.CanComplete(order.PaymentStatus, order.OrderItems.Select(x => x.DeliveryStatus)))
            {
                order.Status = (byte)OrderStatus.Completed;
                order.CompletedAt = now;
            }

            order.UpdatedAt = now;

            var history = new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                FromStatus = (byte)OrderStatus.Processing,
                ToStatus = order.Status,
                ChangedByUserId = deliveredByUserId,
                Note = "تحویل خودکار کد گیفت کارت پس از تایید پرداخت.",
                CreatedAt = now
            };

            await _dbContext.OrderStatusHistories.AddAsync(history);

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation(
                "Gift-code delivery completed for order {OrderNumber}. DeliveredCount={DeliveredCount} ElapsedMs={ElapsedMs} EventType={EventType}",
                order.OrderNumber, soldReservations.Count, stopwatch.ElapsedMilliseconds, "GiftCodeDelivered");
        }

        /// <summary>
        /// Releases only the immutable paid allocation already owned by a
        /// satisfied KYC item. It intentionally never selects available stock.
        /// </summary>
        public async Task<bool> DeliverSatisfiedOrderItemAsync(
            Guid orderItemId,
            Guid? deliveredByUserId = null,
            CancellationToken cancellationToken = default)
        {
            if (orderItemId == Guid.Empty)
                throw new BusinessException("شناسه آیتم سفارش معتبر نیست.");

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancellationToken);
            try
            {
                await SqlServerTransactionLock.AcquireAsync(
                    _dbContext, $"kyc-release:item:{orderItemId:N}", cancellationToken);

                var item = await _dbContext.OrderItems
                    .Include(x => x.Order).ThenInclude(x => x.OrderItems)
                    .Include(x => x.KycLifecycleState)
                    .Include(x => x.OrderItemDeliveries)
                    .FirstOrDefaultAsync(x => x.Id == orderItemId, cancellationToken)
                    ?? throw new NotFoundException("آیتم سفارش یافت نشد.");

                if (item.Order.PaymentStatus != (byte)PaymentStatus.Paid ||
                    item.DeliveryType != (byte)DeliveryType.Instant ||
                    item.KycLifecycleState?.Status != (byte)OrderItemKycStatus.Satisfied)
                {
                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                var allocations = await _dbContext.GiftCodeReservations
                    .Include(x => x.GiftCode)
                    .Where(x => x.OrderId == item.OrderId && x.OrderItemId == item.Id)
                    .ToListAsync(cancellationToken);
                var paidAllocations = allocations.Where(x =>
                    x.Status == (byte)GiftCodeReservationStatus.Sold &&
                    x.GiftCode.Status is (byte)GiftCodeStatus.Sold or (byte)GiftCodeStatus.Delivered &&
                    x.GiftCode.OrderItemId == item.Id).ToList();

                var existingDeliveries = item.OrderItemDeliveries
                    .Where(x => x.DeliveryType == (byte)DeliveryType.Instant)
                    .ToList();
                if (existingDeliveries.Count != 0)
                {
                    var exactExistingDelivery = existingDeliveries.Count == item.Quantity &&
                        existingDeliveries.All(x => x.GiftCodeId.HasValue &&
                            paidAllocations.Any(a => a.GiftCodeId == x.GiftCodeId));
                    if (!exactExistingDelivery)
                        throw new BusinessException("تحویل قبلی آیتم با تخصیص پرداخت‌شده سازگار نیست و نیاز به بررسی دارد.");

                    await transaction.CommitAsync(cancellationToken);
                    return false;
                }

                if (paidAllocations.Count != item.Quantity || allocations.Count != item.Quantity)
                    throw new BusinessException("تخصیص پرداخت‌شده کد برای این آیتم کامل نیست و نیاز به بررسی دارد.");

                var allocationCodeIds = paidAllocations.Select(x => x.GiftCodeId).ToList();
                if (await _dbContext.OrderItemDeliveries.AnyAsync(x =>
                        x.GiftCodeId.HasValue && allocationCodeIds.Contains(x.GiftCodeId.Value) &&
                        x.OrderItemId != item.Id, cancellationToken))
                    throw new BusinessException("یکی از کدهای تخصیص‌یافته قبلاً به آیتم دیگری تحویل شده است.");

                var now = DateTime.UtcNow;
                foreach (var allocation in paidAllocations)
                {
                    var plaintext = _encryptionService.Decrypt(allocation.GiftCode.EncryptedCode);
                    await _dbContext.OrderItemDeliveries.AddAsync(new OrderItemDelivery
                    {
                        Id = Guid.NewGuid(),
                        OrderItemId = item.Id,
                        DeliveryType = (byte)DeliveryType.Instant,
                        GiftCodeId = allocation.GiftCodeId,
                        DeliveredContent = _encryptionService.Encrypt(plaintext),
                        ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext))),
                        EncryptionVersion = 2,
                        IsVisibleToCustomer = true,
                        DeliveredByUserId = deliveredByUserId,
                        CreatedAt = now
                    }, cancellationToken);
                    allocation.GiftCode.Status = (byte)GiftCodeStatus.Delivered;
                    allocation.GiftCode.DeliveredAt = now;
                    allocation.GiftCode.UpdatedAt = now;
                }

                item.DeliveryStatus = (byte)DeliveryStatus.Delivered;
                item.DeliveredAt = now;
                if (OrderFulfillmentRules.CanComplete(item.Order.PaymentStatus,
                        item.Order.OrderItems.Select(x => x.DeliveryStatus)))
                {
                    item.Order.Status = (byte)OrderStatus.Completed;
                    item.Order.CompletedAt ??= now;
                }
                item.Order.UpdatedAt = now;
                await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
                {
                    Id = Guid.NewGuid(), OrderId = item.OrderId,
                    FromStatus = (byte)OrderStatus.Processing, ToStatus = item.Order.Status,
                    ChangedByUserId = deliveredByUserId,
                    Note = "تحویل کد پس از تکمیل احراز هویت آیتم ثبت شد.", CreatedAt = now
                }, cancellationToken);
                await _dbContext.FinancialAuditLogs.AddAsync(new FinancialAuditLog
                {
                    EventType = "GiftCodeDelivered", EntityType = "OrderItemDelivery",
                    EntityId = item.Id, UserId = deliveredByUserId, CorrelationId = item.OrderId,
                    Detail = $"kyc-release:item:{item.Id:N};quantity:{item.Quantity}", CreatedAt = now
                }, cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                _logger.LogInformation(
                    "KYC-satisfied instant item released. OrderId={OrderId} OrderItemId={OrderItemId} Quantity={Quantity}",
                    item.OrderId, item.Id, item.Quantity);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }
}
