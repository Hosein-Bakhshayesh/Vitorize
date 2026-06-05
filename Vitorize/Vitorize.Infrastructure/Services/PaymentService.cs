using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Enums;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class PaymentService : IPaymentService
    {
        private readonly VitorizeDbContext _dbContext;

        public PaymentService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<PaymentStartResultDto> StartPaymentAsync(
            Guid userId,
            Guid orderId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            var order = await _dbContext.Orders
                .Include(x => x.Payments)
                .FirstOrDefaultAsync(x =>
                    x.Id == orderId &&
                    x.UserId == userId);

            if (order == null)
                throw new NotFoundException("سفارش یافت نشد.");

            if (order.PaymentStatus == (byte)PaymentStatus.Paid)
                throw new BusinessException("این سفارش قبلاً پرداخت شده است.");

            var payment = order.Payments
                .Where(x => x.Status == (byte)PaymentStatus.Pending)
                .OrderByDescending(x => x.RequestedAt)
                .FirstOrDefault();

            if (payment == null)
                throw new BusinessException("پرداخت معلق برای این سفارش یافت نشد.");

            if (payment.Amount != order.FinalAmount)
                throw new BusinessException("مبلغ پرداخت با مبلغ سفارش همخوانی ندارد.");

            payment.Authority ??= $"MOCK-{Guid.NewGuid():N}";
            payment.UpdatedAt = DateTime.UtcNow;

            await _dbContext.SaveChangesAsync();

            return new PaymentStartResultDto
            {
                PaymentId = payment.Id,
                OrderId = order.Id,
                Amount = payment.Amount,
                Gateway = payment.Gateway,
                Authority = payment.Authority,
                PaymentUrl = $"/mock-payment/pay?paymentId={payment.Id}"
            };
        }

        public async Task<PaymentVerifyResultDto> VerifyMockPaymentAsync(
            Guid userId,
            Guid paymentId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            await using var transaction =
                await _dbContext.Database.BeginTransactionAsync();

            try
            {
                var payment = await _dbContext.Payments
                    .Include(x => x.Order)
                        .ThenInclude(x => x.GiftCodeReservations)
                    .Include(x => x.Order)
                        .ThenInclude(x => x.OrderItems)
                    .FirstOrDefaultAsync(x =>
                        x.Id == paymentId &&
                        x.UserId == userId);

                if (payment == null)
                    throw new NotFoundException("پرداخت یافت نشد.");

                var order = payment.Order;

                if (payment.Status == (byte)PaymentStatus.Paid)
                {
                    return new PaymentVerifyResultDto
                    {
                        PaymentId = payment.Id,
                        OrderId = order.Id,
                        IsPaid = true,
                        ReferenceNumber = payment.ReferenceNumber,
                        PaymentStatus = payment.Status,
                        OrderStatus = order.Status
                    };
                }

                if (payment.Status != (byte)PaymentStatus.Pending)
                    throw new BusinessException("وضعیت پرداخت قابل تایید نیست.");

                if (payment.Amount != order.FinalAmount)
                    throw new BusinessException("مبلغ پرداخت معتبر نیست.");

                var now = DateTime.UtcNow;

                payment.Status = (byte)PaymentStatus.Paid;
                payment.CallbackVerified = true;
                payment.VerifiedAt = now;
                payment.UpdatedAt = now;
                payment.ReferenceNumber = $"MOCK-REF-{DateTime.UtcNow:yyyyMMddHHmmss}";

                order.PaymentStatus = (byte)PaymentStatus.Paid;
                order.Status = (byte)OrderStatus.Processing;
                order.PaidAt = now;
                order.UpdatedAt = now;

                var activeReservations = order.GiftCodeReservations
                    .Where(x => x.Status == (byte)GiftCodeReservationStatus.Active)
                    .ToList();

                if (!activeReservations.Any())
                    throw new BusinessException("رزرو فعالی برای این سفارش یافت نشد.");

                foreach (var reservation in activeReservations)
                {
                    reservation.Status = (byte)GiftCodeReservationStatus.Sold;
                    reservation.SoldAt = now;

                    var giftCode = await _dbContext.GiftCodes
                        .FirstOrDefaultAsync(x => x.Id == reservation.GiftCodeId);

                    if (giftCode == null)
                        throw new BusinessException("کد رزرو شده یافت نشد.");

                    giftCode.Status = (byte)GiftCodeStatus.Sold;
                    giftCode.SoldAt = now;
                    giftCode.ReservationExpiresAt = null;
                    giftCode.UpdatedAt = now;
                }

                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();

                return new PaymentVerifyResultDto
                {
                    PaymentId = payment.Id,
                    OrderId = order.Id,
                    IsPaid = true,
                    ReferenceNumber = payment.ReferenceNumber,
                    PaymentStatus = payment.Status,
                    OrderStatus = order.Status
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