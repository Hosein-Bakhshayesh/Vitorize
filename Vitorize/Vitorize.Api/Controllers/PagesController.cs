using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/pages")]
    public class PagesController : ControllerBase
    {
        private readonly IStorefrontService _storefrontService;

        public PagesController(IStorefrontService storefrontService)
        {
            _storefrontService = storefrontService;
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<ApiResult<PageDto>>> GetBySlug(string slug)
        {
            var result = await _storefrontService.GetPageBySlugAsync(slug);

            return Ok(ApiResult<PageDto>.Success(
                result,
                "صفحه با موفقیت دریافت شد."));
        }
    }
}