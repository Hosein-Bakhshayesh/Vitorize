using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    /// <summary>
    /// Admin blog management. Uses the same content-management policy as CMS pages so blog authoring
    /// is available to the roles that already administer storefront content.
    /// </summary>
    [ApiController]
    [Authorize(Policy = "SettingsManage")]
    [Route("api/admin/blog")]
    public class AdminBlogController : ControllerBase
    {
        private readonly IAdminBlogService _blogService;

        public AdminBlogController(IAdminBlogService blogService)
        {
            _blogService = blogService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminBlogPostListItemDto>>>> GetAll()
        {
            var result = await _blogService.GetAllAsync();

            return Ok(ApiResult<List<AdminBlogPostListItemDto>>.Success(
                result,
                "لیست مطالب وبلاگ با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBlogPostDto>>> GetById(Guid id)
        {
            var result = await _blogService.GetByIdAsync(id);

            return Ok(ApiResult<AdminBlogPostDto>.Success(result, "مطلب با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminBlogPostDto>>> Create(CreateBlogPostRequestDto request)
        {
            var result = await _blogService.CreateAsync(request);

            return Ok(ApiResult<AdminBlogPostDto>.Success(result, "مطلب با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBlogPostDto>>> Update(Guid id, UpdateBlogPostRequestDto request)
        {
            var result = await _blogService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminBlogPostDto>.Success(result, "مطلب با موفقیت ویرایش شد."));
        }

        [HttpPost("{id:guid}/publish")]
        public async Task<ActionResult<ApiResult<AdminBlogPostDto>>> Publish(Guid id)
        {
            var result = await _blogService.SetPublishedAsync(id, true);

            return Ok(ApiResult<AdminBlogPostDto>.Success(result, "مطلب منتشر شد."));
        }

        [HttpPost("{id:guid}/unpublish")]
        public async Task<ActionResult<ApiResult<AdminBlogPostDto>>> Unpublish(Guid id)
        {
            var result = await _blogService.SetPublishedAsync(id, false);

            return Ok(ApiResult<AdminBlogPostDto>.Success(result, "مطلب از انتشار خارج شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _blogService.DeleteAsync(id);

            return Ok(ApiResult.Success("مطلب با موفقیت حذف شد."));
        }
    }
}
