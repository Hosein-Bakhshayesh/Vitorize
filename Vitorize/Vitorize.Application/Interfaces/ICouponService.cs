using Vitorize.Application.DTOs.Coupons;

namespace Vitorize.Application.Interfaces
{
    public interface ICouponService
    {
        Task<ValidateCouponResultDto> ValidateAsync(
            Guid userId,
            ValidateCouponRequestDto request);
    }
}