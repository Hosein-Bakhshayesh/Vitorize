using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Coupons;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Infrastructure.Services
{
    public class AdminCouponService : IAdminCouponService
    {
        private readonly VitorizeDbContext _dbContext;

        public AdminCouponService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<List<CouponDto>> GetAllAsync()
        {
            return await _dbContext.Coupons
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => MapCoupon(x))
                .ToListAsync();
        }

        public async Task<CouponDto> GetByIdAsync(Guid couponId)
        {
            var coupon = await _dbContext.Coupons
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == couponId);

            if (coupon == null)
                throw new NotFoundException("کپن تخفیف یافت نشد.");

            return MapCoupon(coupon);
        }

        public async Task<CouponDto> CreateAsync(AdminCouponCreateDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Code))
                throw new BusinessException("کد تخفیف الزامی است.");

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان کپن الزامی است.");

            if (request.DiscountValue <= 0)
                throw new BusinessException("مقدار تخفیف باید بزرگتر از صفر باشد.");

            var code = request.Code.Trim().ToUpperInvariant();

            var exists = await _dbContext.Coupons
                .AnyAsync(x => x.Code == code);

            if (exists)
                throw new BusinessException("این کد تخفیف قبلاً ثبت شده است.");

            var coupon = new Coupon
            {
                Id = Guid.NewGuid(),
                Code = code,
                Title = request.Title.Trim(),
                DiscountType = request.DiscountType,
                DiscountValue = request.DiscountValue,
                MaxUsageCount = request.MaxUsageCount,
                MaxUsagePerUser = request.MaxUsagePerUser,
                MinOrderAmount = request.MinOrderAmount,
                StartsAt = request.StartsAt,
                EndsAt = request.EndsAt,
                UsedCount = 0,
                IsActive = request.IsActive,
                CreatedAt = DateTime.UtcNow
            };

            await _dbContext.Coupons.AddAsync(coupon);
            await _dbContext.SaveChangesAsync();

            return MapCoupon(coupon);
        }

        public async Task<CouponDto> UpdateAsync(
            Guid couponId,
            AdminCouponUpdateDto request)
        {
            var coupon = await _dbContext.Coupons
                .FirstOrDefaultAsync(x => x.Id == couponId);

            if (coupon == null)
                throw new NotFoundException("کپن تخفیف یافت نشد.");

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new BusinessException("عنوان کپن الزامی است.");

            if (request.DiscountValue <= 0)
                throw new BusinessException("مقدار تخفیف باید بزرگتر از صفر باشد.");

            coupon.Title = request.Title.Trim();
            coupon.DiscountType = request.DiscountType;
            coupon.DiscountValue = request.DiscountValue;
            coupon.MaxUsageCount = request.MaxUsageCount;
            coupon.MaxUsagePerUser = request.MaxUsagePerUser;
            coupon.MinOrderAmount = request.MinOrderAmount;
            coupon.StartsAt = request.StartsAt;
            coupon.EndsAt = request.EndsAt;
            coupon.IsActive = request.IsActive;

            await _dbContext.SaveChangesAsync();

            return MapCoupon(coupon);
        }

        public async Task DeleteAsync(Guid couponId)
        {
            var coupon = await _dbContext.Coupons
                .Include(x => x.CouponUsages)
                .FirstOrDefaultAsync(x => x.Id == couponId);

            if (coupon == null)
                throw new NotFoundException("کپن تخفیف یافت نشد.");

            if (coupon.CouponUsages.Any())
                throw new BusinessException("این کپن استفاده شده و قابل حذف نیست.");

            _dbContext.Coupons.Remove(coupon);

            await _dbContext.SaveChangesAsync();
        }

        private static CouponDto MapCoupon(Coupon coupon)
        {
            return new CouponDto
            {
                Id = coupon.Id,
                Code = coupon.Code,
                Title = coupon.Title,
                DiscountType = coupon.DiscountType,
                DiscountValue = coupon.DiscountValue,
                MaxUsageCount = coupon.MaxUsageCount,
                UsedCount = coupon.UsedCount,
                MaxUsagePerUser = coupon.MaxUsagePerUser,
                MinOrderAmount = coupon.MinOrderAmount,
                StartsAt = coupon.StartsAt,
                EndsAt = coupon.EndsAt,
                IsActive = coupon.IsActive,
                CreatedAt = coupon.CreatedAt
            };
        }
    }
}