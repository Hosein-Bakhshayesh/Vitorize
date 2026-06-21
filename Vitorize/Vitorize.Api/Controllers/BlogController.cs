using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Storefront;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/blog")]
    public class BlogController : ControllerBase
    {
        private readonly IStorefrontService _storefrontService;

        public BlogController(IStorefrontService storefrontService)
        {
            _storefrontService = storefrontService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<StorefrontBlogPostDto>>>> GetAll()
        {
            var result = await _storefrontService.GetBlogPostsAsync();

            return Ok(ApiResult<List<StorefrontBlogPostDto>>.Success(
                result,
                "لیست مطالب با موفقیت دریافت شد."));
        }

        [HttpGet("{slug}")]
        public async Task<ActionResult<ApiResult<BlogPostDto>>> GetBySlug(string slug)
        {
            var result = await _storefrontService.GetBlogPostBySlugAsync(slug);

            return Ok(ApiResult<BlogPostDto>.Success(
                result,
                "مطلب با موفقیت دریافت شد."));
        }
    }
}