using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/faqs")]
    public class FaqsController : ControllerBase
    {
        private readonly IStorefrontService _storefrontService;

        public FaqsController(IStorefrontService storefrontService)
        {
            _storefrontService = storefrontService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<FaqDto>>>> GetAll()
        {
            var result = await _storefrontService.GetFaqsAsync();

            return Ok(ApiResult<List<FaqDto>>.Success(
                result,
                "سوالات متداول با موفقیت دریافت شد."));
        }
    }
}