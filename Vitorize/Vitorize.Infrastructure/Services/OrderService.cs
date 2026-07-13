using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class OrderService : IOrderService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ISmsOutboxEnqueuer _smsOutbox;

        public OrderService(VitorizeDbContext dbContext, ISmsOutboxEnqueuer smsOutbox)
        {
            _dbContext = dbContext;
            _smsOutbox = smsOutbox;
        }

        public async Task<List<OrderDto>> GetMyOrdersAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task<OrderDto> GetMyOrderDetailsAsync(Guid userId, Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .FirstOrDefaultAsync(x =>
                    x.Id == orderId &&
                    x.UserId == userId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            return MapOrderDetails(order);
        }

        public async Task<List<DeliveredCodeDto>> GetMyDeliveredCodesAsync(Guid userId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            return await _dbContext.OrderItemDeliveries
                .AsNoTracking()
                .Where(x =>
                    x.IsVisibleToCustomer &&
                    x.OrderItem.Order.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new DeliveredCodeDto
                {
                    Id = x.Id,
                    OrderId = x.OrderItem.OrderId,
                    OrderNumber = x.OrderItem.Order.OrderNumber,
                    OrderItemId = x.OrderItemId,
                    ProductId = x.OrderItem.ProductId,
                    ProductTitle = x.OrderItem.ProductTitle,
                    VariantTitle = x.OrderItem.VariantTitle,
                    DeliveryType = x.DeliveryType,
                    DeliveredContent = x.DeliveredContent,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<List<OrderDto>> GetAdminOrdersAsync()
        {
            var orders = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task<OrderDto> GetAdminOrderDetailsAsync(Guid orderId)
        {
            var order = await _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            return MapOrderDetails(order);
        }

        public async Task<List<OrderDto>> SearchAdminOrdersAsync(AdminOrderFilterDto filter)
        {
            filter ??= new AdminOrderFilterDto();

            var query = _dbContext.Orders
                .AsNoTracking()
                .Include(x => x.OrderItems)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(filter.OrderNumber))
            {
                var orderNumber = filter.OrderNumber.Trim();

                query = query.Where(x =>
                    x.OrderNumber.Contains(orderNumber));
            }

            if (filter.UserId.HasValue)
            {
                query = query.Where(x =>
                    x.UserId == filter.UserId.Value);
            }

            if (filter.Status.HasValue)
            {
                query = query.Where(x =>
                    x.Status == filter.Status.Value);
            }

            if (filter.PaymentStatus.HasValue)
            {
                query = query.Where(x =>
                    x.PaymentStatus == filter.PaymentStatus.Value);
            }

            if (filter.FromDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt >= filter.FromDate.Value);
            }

            if (filter.ToDate.HasValue)
            {
                query = query.Where(x =>
                    x.CreatedAt <= filter.ToDate.Value);
            }

            var orders = await query
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return orders.Select(MapOrderSummary).ToList();
        }

        public async Task CancelOrderAsync(
            Guid orderId,
            Guid adminUserId,
            string? reason)
        {
            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            if (adminUserId == Guid.Empty)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems)
                    .ThenInclude(x => x.OrderItemDeliveries)
                .Include(x => x.GiftCodeReservations)
                    .ThenInclude(x => x.GiftCode)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.Status == (byte)OrderStatus.Completed)
                throw new BusinessException("سفارش تکمیل شده قابل لغو نیست.");

            if (order.Status == (byte)OrderStatus.Cancelled)
                throw new BusinessException("این سفارش قبلاً لغو شده است.");

            var hasDelivery = order.OrderItems
                .Any(x => x.OrderItemDeliveries.Any());

            if (hasDelivery)
                throw new BusinessException("سفارشی که کد آن تحویل شده قابل لغو نیست.");

            var now = DateTime.UtcNow;
            var fromStatus = order.Status;

            foreach (var reservation in order.GiftCodeReservations)
            {
                if (reservation.Status == (byte)GiftCodeReservationStatus.Active)
                {
                    reservation.Status = (byte)GiftCodeReservationStatus.Released;
                    reservation.ReleasedAt = now;

                    reservation.GiftCode.Status = (byte)GiftCodeStatus.Available;
                    reservation.GiftCode.ReservedByUserId = null;
                    reservation.GiftCode.ReservedAt = null;
                    reservation.GiftCode.ReservationExpiresAt = null;
                    reservation.GiftCode.UpdatedAt = now;
                }
            }

            order.Status = (byte)OrderStatus.Cancelled;
            order.AdminNote = reason;
            order.UpdatedAt = now;

            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = order.Status,
                ChangedByUserId = adminUserId,
                Note = string.IsNullOrWhiteSpace(reason)
                    ? "سفارش توسط ادمین لغو شد."
                    : reason.Trim(),
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync();
        }

        public async Task CompleteOrderAsync(Guid orderId, Guid adminUserId)
        {
            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            if (adminUserId == Guid.Empty)
                throw new UnauthorizedException("ادمین احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .Include(x => x.OrderItems)
                .FirstOrDefaultAsync(x => x.Id == orderId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.Status == (byte)OrderStatus.Completed)
                throw new BusinessException("این سفارش قبلاً تکمیل شده است.");

            if (order.Status == (byte)OrderStatus.Cancelled)
                throw new BusinessException("سفارش لغو شده قابل تکمیل نیست.");

            if (order.PaymentStatus != (byte)PaymentStatus.Paid)
                throw new BusinessException("سفارش پرداخت نشده قابل تکمیل نیست.");

            var now = DateTime.UtcNow;
            var fromStatus = order.Status;

            order.Status = (byte)OrderStatus.Completed;
            order.CompletedAt ??= now;
            order.UpdatedAt = now;

            await _dbContext.OrderStatusHistories.AddAsync(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                FromStatus = fromStatus,
                ToStatus = order.Status,
                ChangedByUserId = adminUserId,
                Note = "سفارش توسط ادمین تکمیل شد.",
                CreatedAt = now
            });

            var mobile = await _dbContext.Users
                .Where(x => x.Id == order.UserId)
                .Select(x => x.Mobile)
                .FirstOrDefaultAsync();

            await _smsOutbox.EnqueueTemplateAsync(
                mobile,
                Vitorize.Application.Common.SmsTemplateKeys.OrderCompleted,
                new[]
                {
                    new Vitorize.Application.Models.Sms.SmsTemplateParameter(
                        Vitorize.Application.Common.SmsTemplateParams.Order, order.OrderNumber)
                },
                purpose: "OrderCompleted",
                aggregateId: order.Id);

            await _dbContext.SaveChangesAsync();
        }

        private static OrderDto MapOrderSummary(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                SubtotalAmount = order.SubtotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                CompletedAt = order.CompletedAt,
                Items = order.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductTitle = i.ProductTitle,
                    VariantTitle = i.VariantTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    DeliveryType = i.DeliveryType,
                    DeliveryStatus = i.DeliveryStatus,
                    RequiresVerification = i.RequiresVerification,
                    CreatedAt = i.CreatedAt,
                    DeliveredAt = i.DeliveredAt
                }).ToList()
            };
        }

        private static OrderDto MapOrderDetails(Order order)
        {
            return new OrderDto
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                Status = order.Status,
                PaymentStatus = order.PaymentStatus,
                SubtotalAmount = order.SubtotalAmount,
                DiscountAmount = order.DiscountAmount,
                FinalAmount = order.FinalAmount,
                CreatedAt = order.CreatedAt,
                PaidAt = order.PaidAt,
                CompletedAt = order.CompletedAt,
                Items = order.OrderItems.Select(i => new OrderItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductVariantId = i.ProductVariantId,
                    ProductTitle = i.ProductTitle,
                    VariantTitle = i.VariantTitle,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice,
                    TotalPrice = i.TotalPrice,
                    DeliveryType = i.DeliveryType,
                    DeliveryStatus = i.DeliveryStatus,
                    RequiresVerification = i.RequiresVerification,
                    CreatedAt = i.CreatedAt,
                    DeliveredAt = i.DeliveredAt,
                    Deliveries = i.OrderItemDeliveries
                        .Where(d => d.IsVisibleToCustomer)
                        .Select(d => new OrderDeliveryDto
                        {
                            Id = d.Id,
                            OrderItemId = d.OrderItemId,
                            DeliveryType = d.DeliveryType,
                            GiftCodeId = d.GiftCodeId,
                            DeliveredContent = d.DeliveredContent,
                            IsVisibleToCustomer = d.IsVisibleToCustomer,
                            CreatedAt = d.CreatedAt
                        })
                        .ToList()
                }).ToList()
            };
        }
    }
}