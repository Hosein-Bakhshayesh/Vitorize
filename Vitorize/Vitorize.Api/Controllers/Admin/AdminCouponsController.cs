using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Coupons;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/coupons")]
    [Authorize(Policy = "AdminOnly")]
    public class AdminCouponsController : ControllerBase
    {
        private readonly IAdminCouponService _adminCouponService;

        public AdminCouponsController(IAdminCouponService adminCouponService)
        {
            _adminCouponService = adminCouponService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<CouponDto>>>> GetAll()
        {
            var result = await _adminCouponService.GetAllAsync();

            return Ok(ApiResult<List<CouponDto>>.Success(
                result,
                "لیست کدهای تخفیف با موفقیت دریافت شد."));
        }

        [HttpGet("{couponId:guid}")]
        public async Task<ActionResult<ApiResult<CouponDto>>> GetById(Guid couponId)
        {
            var result = await _adminCouponService.GetByIdAsync(couponId);

            return Ok(ApiResult<CouponDto>.Success(
                result,
                "کد تخفیف با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<CouponDto>>> Create(
            AdminCouponCreateDto request)
        {
            var result = await _adminCouponService.CreateAsync(request);

            return Ok(ApiResult<CouponDto>.Success(
                result,
                "کد تخفیف با موفقیت ایجاد شد."));
        }

        [HttpPut("{couponId:guid}")]
        public async Task<ActionResult<ApiResult<CouponDto>>> Update(
            Guid couponId,
            AdminCouponUpdateDto request)
        {
            var result = await _adminCouponService.UpdateAsync(
                couponId,
                request);

            return Ok(ApiResult<CouponDto>.Success(
                result,
                "کد تخفیف با موفقیت بروزرسانی شد."));
        }

        [HttpDelete("{couponId:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid couponId)
        {
            await _adminCouponService.DeleteAsync(couponId);

            return Ok(ApiResult.Success(
                "کد تخفیف با موفقیت حذف شد."));
        }
    }
}