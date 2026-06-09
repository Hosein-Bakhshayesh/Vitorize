using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class CheckoutService : ICheckoutService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ICouponService _couponService;

        public CheckoutService(
            VitorizeDbContext dbContext,
            ICouponService couponService)
        {
            _dbContext = dbContext;
            _couponService = couponService;
        }

        public async Task<CheckoutResultDto> CheckoutAsync(
            Guid userId,
            CheckoutRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            request ??= new CheckoutRequestDto();

            var cart = await _dbContext.Carts
                .Include(x => x.CartItems)
                    .ThenInclude(x => x.Product)
                .Include(x => x.CartItems)
                    .ThenInclude(x => x.ProductVariant)
                .FirstOrDefaultAsync(x => x.UserId == userId);

            if (cart == null || !cart.CartItems.Any())
                throw new BusinessException("سبد خرید خالی است.");

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var now = DateTime.UtcNow;

                var subtotalAmount = cart.CartItems.Sum(x =>
                    x.UnitPrice * x.Quantity);

                var discountAmount = 0m;
                Guid? couponId = null;

                if (!string.IsNullOrWhiteSpace(request.CouponCode))
                {
                    var couponResult = await _couponService.ValidateAsync(
                        userId,
                        new Vitorize.Application.DTOs.Coupons.ValidateCouponRequestDto
                        {
                            Code = request.CouponCode,
                            OrderAmount = subtotalAmount
                        });

                    couponId = couponResult.CouponId;
                    discountAmount = couponResult.DiscountAmount;
                }

                var finalAmount = subtotalAmount - discountAmount;

                if (finalAmount < 0)
                    finalAmount = 0;

                var order = new Order
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    OrderNumber = $"VT-{DateTime.UtcNow:yyyyMMddHHmmss}",
                    Status = (byte)OrderStatus.PendingPayment,
                    PaymentStatus = (byte)PaymentStatus.Pending,
                    SubtotalAmount = subtotalAmount,
                    DiscountAmount = discountAmount,
                    FinalAmount = finalAmount,
                    CouponId = couponId,
                    Description = request.Description,
                    CreatedAt = now
                };

                await _dbContext.Orders.AddAsync(order);

                var orderItems = new List<OrderItem>();

                foreach (var cartItem in cart.CartItems)
                {
                    var orderItem = new OrderItem
                    {
                        Id = Guid.NewGuid(),
                        OrderId = order.Id,
                        ProductId = cartItem.ProductId,
                        ProductVariantId = cartItem.ProductVariantId,
                        ProductTitle = cartItem.Product.Title,
                        VariantTitle = cartItem.ProductVariant?.Title,
                        Quantity = cartItem.Quantity,
                        UnitPrice = cartItem.UnitPrice,
                        TotalPrice = cartItem.UnitPrice * cartItem.Quantity,
                        DeliveryType = cartItem.Product.DeliveryType,
                        DeliveryStatus = (byte)DeliveryStatus.Pending,
                        RequiresVerification = cartItem.Product.RequiresVerification,
                        CreatedAt = now
                    };

                    orderItems.Add(orderItem);
                }

                await _dbContext.OrderItems.AddRangeAsync(orderItems);

                var reservationIds = new List<Guid>();
                var reservationExpiresAt = now.AddMinutes(15);

                foreach (var orderItem in orderItems)
                {
                    if (orderItem.DeliveryType != (byte)DeliveryType.Instant)
                        continue;

                    for (var i = 0; i < orderItem.Quantity; i++)
                    {
                        var giftCode = await _dbContext.GiftCodes
                            .Where(x =>
                                x.ProductId == orderItem.ProductId &&
                                x.ProductVariantId == orderItem.ProductVariantId &&
                                x.Status == (byte)GiftCodeStatus.Available)
                            .OrderBy(x => x.CreatedAt)
                            .FirstOrDefaultAsync();

                        if (giftCode == null)
                            throw new BusinessException(
                                $"موجودی کد برای محصول {orderItem.ProductTitle} کافی نیست.");

                        giftCode.Status = (byte)GiftCodeStatus.Reserved;
                        giftCode.ReservedByUserId = userId;
                        giftCode.ReservedAt = now;
                        giftCode.ReservationExpiresAt = reservationExpiresAt;
                        giftCode.OrderItemId = orderItem.Id;
                        giftCode.UpdatedAt = now;

                        var reservation = new GiftCodeReservation
                        {
                            Id = Guid.NewGuid(),
                            UserId = userId,
                            OrderId = order.Id,
                            OrderItemId = orderItem.Id,
                            ProductId = orderItem.ProductId,
                            ProductVariantId = orderItem.ProductVariantId,
                            GiftCodeId = giftCode.Id,
                            Status = (byte)GiftCodeReservationStatus.Active,
                            ReservedAt = now,
                            ExpiresAt = reservationExpiresAt
                        };

                        reservationIds.Add(reservation.Id);

                        await _dbContext.GiftCodeReservations.AddAsync(reservation);
                    }
                }

                var payment = new Payment
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = userId,
                    Amount = finalAmount,
                    Gateway = "Mock",
                    Status = (byte)PaymentStatus.Pending,
                    CallbackVerified = false,
                    RequestedAt = now
                };

                await _dbContext.Payments.AddAsync(payment);

                _dbContext.CartItems.RemoveRange(cart.CartItems);

                await _dbContext.SaveChangesAsync();

                await transaction.CommitAsync();

                return new CheckoutResultDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    SubtotalAmount = order.SubtotalAmount,
                    DiscountAmount = order.DiscountAmount,
                    FinalAmount = order.FinalAmount,
                    OrderStatus = order.Status,
                    PaymentStatus = order.PaymentStatus,
                    ReservationIds = reservationIds
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}