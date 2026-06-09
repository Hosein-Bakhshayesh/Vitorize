using Microsoft.EntityFrameworkCore;
using System.Data;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class CouponService : ICouponService
    {
        private readonly VitorizeDbContext _dbContext;

        public CouponService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<ValidateCouponResultDto> ValidateAsync(
            Guid userId,
            ValidateCouponRequestDto request)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (request == null)
                throw new BusinessException("درخواست معتبر نیست.");

            if (string.IsNullOrWhiteSpace(request.Code))
                throw new BusinessException("کد تخفیف الزامی است.");

            if (request.OrderAmount <= 0)
                throw new BusinessException("مبلغ سفارش معتبر نیست.");

            var now = DateTime.UtcNow;
            var code = NormalizeCode(request.Code);

            var coupon = await _dbContext.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == code);

            if (coupon == null)
                throw new NotFoundException("کد تخفیف یافت نشد.");

            await ValidateCouponRulesAsync(
                coupon.Id,
                userId,
                request.OrderAmount,
                now);

            var discountAmount = CalculateDiscount(
                request.OrderAmount,
                coupon.DiscountType,
                coupon.DiscountValue);

            var finalAmount = request.OrderAmount - discountAmount;

            if (finalAmount < 0)
                finalAmount = 0;

            return new ValidateCouponResultDto
            {
                CouponId = coupon.Id,
                Code = coupon.Code,
                OrderAmount = request.OrderAmount,
                DiscountAmount = discountAmount,
                FinalAmount = finalAmount
            };
        }

        public async Task MarkCouponAsUsedAsync(
            Guid userId,
            Guid orderId,
            Guid couponId)
        {
            if (userId == Guid.Empty)
                throw new UnauthorizedException("کاربر احراز هویت نشده است.");

            if (orderId == Guid.Empty)
                throw new BusinessException("شناسه سفارش معتبر نیست.");

            if (couponId == Guid.Empty)
                throw new BusinessException("شناسه کد تخفیف معتبر نیست.");

            var hasCurrentTransaction =
                _dbContext.Database.CurrentTransaction != null;

            await using var transaction = hasCurrentTransaction
                ? null
                : await _dbContext.Database.BeginTransactionAsync(
                    IsolationLevel.Serializable);

            try
            {
                var alreadyUsed = await _dbContext.CouponUsages
                    .AnyAsync(x =>
                        x.OrderId == orderId &&
                        x.CouponId == couponId &&
                        x.UserId == userId);

                if (alreadyUsed)
                {
                    if (transaction != null)
                        await transaction.CommitAsync();

                    return;
                }

                var order = await _dbContext.Orders
                    .FirstOrDefaultAsync(x =>
                        x.Id == orderId &&
                        x.UserId == userId);

                if (order == null)
                    throw new NotFoundException("سفارش یافت نشد.");

                if (order.CouponId != couponId)
                    throw new BusinessException("کد تخفیف متعلق به این سفارش نیست.");

                if (order.FinalAmount < 0)
                    throw new BusinessException("مبلغ سفارش معتبر نیست.");

                await ValidateCouponRulesAsync(
                    couponId,
                    userId,
                    order.SubtotalAmount,
                    DateTime.UtcNow);

                var coupon = await _dbContext.Coupons
                    .FirstOrDefaultAsync(x => x.Id == couponId);

                if (coupon == null)
                    throw new NotFoundException("کد تخفیف یافت نشد.");

                coupon.UsedCount += 1;

                await _dbContext.CouponUsages.AddAsync(new Vitorize.Domain.Entities.CouponUsage
                {
                    Id = Guid.NewGuid(),
                    CouponId = couponId,
                    UserId = userId,
                    OrderId = orderId,
                    UsedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();

                if (transaction != null)
                    await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null)
                    await transaction.RollbackAsync();

                throw;
            }
        }

        private async Task ValidateCouponRulesAsync(
            Guid couponId,
            Guid userId,
            decimal orderAmount,
            DateTime now)
        {
            var coupon = await _dbContext.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == couponId);

            if (coupon == null)
                throw new NotFoundException("کد تخفیف یافت نشد.");

            if (!coupon.IsActive)
                throw new BusinessException("کد تخفیف غیرفعال است.");

            if (coupon.StartsAt.HasValue && coupon.StartsAt.Value > now)
                throw new BusinessException("زمان استفاده از این کد تخفیف هنوز شروع نشده است.");

            if (coupon.EndsAt.HasValue && coupon.EndsAt.Value < now)
                throw new BusinessException("کد تخفیف منقضی شده است.");

            if (coupon.MinOrderAmount.HasValue &&
                orderAmount < coupon.MinOrderAmount.Value)
                throw new BusinessException("مبلغ سفارش کمتر از حداقل مبلغ مجاز برای این کد تخفیف است.");

            if (coupon.MaxUsageCount.HasValue &&
                coupon.UsedCount >= coupon.MaxUsageCount.Value)
                throw new BusinessException("ظرفیت استفاده از این کد تخفیف تکمیل شده است.");

            if (coupon.MaxUsagePerUser.HasValue)
            {
                var userUsedCount = await _dbContext.CouponUsages
                    .CountAsync(x =>
                        x.CouponId == coupon.Id &&
                        x.UserId == userId);

                if (userUsedCount >= coupon.MaxUsagePerUser.Value)
                    throw new BusinessException("شما قبلاً از این کد تخفیف استفاده کرده‌اید.");
            }

            _ = CalculateDiscount(
                orderAmount,
                coupon.DiscountType,
                coupon.DiscountValue);
        }

        private static decimal CalculateDiscount(
            decimal orderAmount,
            byte discountType,
            decimal discountValue)
        {
            if (discountValue <= 0)
                throw new BusinessException("مقدار تخفیف معتبر نیست.");

            decimal discountAmount;

            if (discountType == (byte)DiscountType.Percentage)
            {
                if (discountValue > 100)
                    throw new BusinessException("درصد تخفیف نمی‌تواند بیشتر از 100 باشد.");

                discountAmount = orderAmount * discountValue / 100;
            }
            else if (discountType == (byte)DiscountType.FixedAmount)
            {
                discountAmount = discountValue;
            }
            else
            {
                throw new BusinessException("نوع تخفیف معتبر نیست.");
            }

            if (discountAmount > orderAmount)
                discountAmount = orderAmount;

            return discountAmount;
        }

        private static string NormalizeCode(string code)
        {
            return code.Trim().ToUpperInvariant();
        }
    }
}