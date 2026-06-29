using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/storefront")]
    public class StorefrontController : ControllerBase
    {
        private readonly IStorefrontService _storefrontService;

        public StorefrontController(IStorefrontService storefrontService)
        {
            _storefrontService = storefrontService;
        }

        [HttpGet("home")]
        public async Task<ActionResult<ApiResult<HomeDto>>> GetHome()
        {
            var result = await _storefrontService.GetHomeAsync();

            return Ok(ApiResult<HomeDto>.Success(
                result,
                "اطلاعات صفحه اصلی با موفقیت دریافت شد."));
        }

        [HttpGet("banners")]
        public async Task<ActionResult<ApiResult<List<BannerDto>>>> GetBanners(
            [FromQuery] string? position)
        {
            var result = await _storefrontService.GetActiveBannersAsync(position);

            return Ok(ApiResult<List<BannerDto>>.Success(
                result,
                "بنرها با موفقیت دریافت شدند."));
        }
    }
}