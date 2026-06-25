using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Banners;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/banners")]
    public class AdminBannersController : ControllerBase
    {
        private readonly IAdminBannerService _bannerService;

        public AdminBannersController(IAdminBannerService bannerService)
        {
            _bannerService = bannerService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminBannerDto>>>> GetAll()
        {
            var result = await _bannerService.GetAllAsync();

            return Ok(ApiResult<List<AdminBannerDto>>.Success(
                result,
                "لیست بنرها با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBannerDto>>> GetById(Guid id)
        {
            var result = await _bannerService.GetByIdAsync(id);

            return Ok(ApiResult<AdminBannerDto>.Success(
                result,
                "بنر با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminBannerDto>>> Create(CreateBannerRequestDto request)
        {
            var result = await _bannerService.CreateAsync(request);

            return Ok(ApiResult<AdminBannerDto>.Success(
                result,
                "بنر با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBannerDto>>> Update(
            Guid id,
            UpdateBannerRequestDto request)
        {
            var result = await _bannerService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminBannerDto>.Success(
                result,
                "بنر با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _bannerService.DeleteAsync(id);

            return Ok(ApiResult.Success("بنر با موفقیت حذف شد."));
        }
    }
}
