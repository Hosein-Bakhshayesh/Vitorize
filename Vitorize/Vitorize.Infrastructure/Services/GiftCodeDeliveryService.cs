using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Diagnostics;
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
            var hasInstantItems = order.OrderItems
                .Any(x => x.DeliveryType == (byte)DeliveryType.Instant);

            if (hasInstantItems && !soldReservations.Any())
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

            var allItemsDelivered = order.OrderItems
                .All(x => x.DeliveryStatus == (byte)DeliveryStatus.Delivered);

            if (allItemsDelivered)
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
    }
}
