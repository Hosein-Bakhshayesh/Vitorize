using Vitorize.Application.DTOs.Admin.Coupons;
using Vitorize.Application.DTOs.Coupons;

namespace Vitorize.Application.Interfaces
{
    public interface IAdminCouponService
    {
        Task<List<CouponDto>> GetAllAsync();

        Task<CouponDto> GetByIdAsync(Guid couponId);

        Task<CouponDto> CreateAsync(AdminCouponCreateDto request);

        Task<CouponDto> UpdateAsync(Guid couponId, AdminCouponUpdateDto request);

        Task DeleteAsync(Guid couponId);
    }
}