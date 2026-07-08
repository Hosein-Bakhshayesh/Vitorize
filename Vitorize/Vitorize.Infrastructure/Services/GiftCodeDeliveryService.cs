using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class GiftCodeDeliveryService : IGiftCodeDeliveryService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly IEncryptionService _encryptionService;

        public GiftCodeDeliveryService(
            VitorizeDbContext dbContext,
            IEncryptionService encryptionService)
        {
            _dbContext = dbContext;
            _encryptionService = encryptionService;
        }

        public async Task DeliverOrderAsync(
            Guid orderId,
            Guid? deliveredByUserId = null)
        {
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
                throw new BusinessException("کد قابل تحویلی برای این سفارش یافت نشد.");

            foreach (var reservation in soldReservations)
            {
                var orderItem = order.OrderItems
                    .FirstOrDefault(x => x.Id == reservation.OrderItemId.Value);

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
                    DeliveredContent = decryptedCode,
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
        }
    }
}