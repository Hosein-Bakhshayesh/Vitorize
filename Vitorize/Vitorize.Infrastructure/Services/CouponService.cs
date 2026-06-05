using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Enums;
using Vitorize.Infrastructure.Persistence;
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

            if (string.IsNullOrWhiteSpace(request.Code))
                throw new BusinessException("کد تخفیف الزامی است.");

            if (request.OrderAmount <= 0)
                throw new BusinessException("مبلغ سفارش معتبر نیست.");

            var now = DateTime.UtcNow;
            var code = request.Code.Trim().ToUpperInvariant();

            var coupon = await _dbContext.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == code);

            if (coupon == null)
                throw new NotFoundException("کد تخفیف یافت نشد.");

            if (!coupon.IsActive)
                throw new BusinessException("کد تخفیف غیرفعال است.");

            if (coupon.StartsAt.HasValue && coupon.StartsAt.Value > now)
                throw new BusinessException("زمان استفاده از این کد تخفیف هنوز شروع نشده است.");

            if (coupon.EndsAt.HasValue && coupon.EndsAt.Value < now)
                throw new BusinessException("کد تخفیف منقضی شده است.");

            if (coupon.MinOrderAmount.HasValue &&
                request.OrderAmount < coupon.MinOrderAmount.Value)
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

        private static decimal CalculateDiscount(
            decimal orderAmount,
            byte discountType,
            decimal discountValue)
        {
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
    }
}