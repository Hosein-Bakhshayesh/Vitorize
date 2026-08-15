using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "SettingsManage")]
    [Route("api/admin/pages")]
    public class AdminPagesController : ControllerBase
    {
        private readonly IAdminPageService _pageService;

        public AdminPagesController(IAdminPageService pageService)
        {
            _pageService = pageService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminPageListItemDto>>>> GetAll()
        {
            var result = await _pageService.GetAllAsync();

            return Ok(ApiResult<List<AdminPageListItemDto>>.Success(
                result,
                "لیست صفحه‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminPageDto>>> GetById(Guid id)
        {
            var result = await _pageService.GetByIdAsync(id);

            return Ok(ApiResult<AdminPageDto>.Success(result, "صفحه با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminPageDto>>> Create(CreatePageRequestDto request)
        {
            var result = await _pageService.CreateAsync(request);

            return Ok(ApiResult<AdminPageDto>.Success(result, "صفحه با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminPageDto>>> Update(Guid id, UpdatePageRequestDto request)
        {
            var result = await _pageService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminPageDto>.Success(result, "صفحه با موفقیت ویرایش شد."));
        }

        [HttpPost("{id:guid}/publish")]
        public async Task<ActionResult<ApiResult<AdminPageDto>>> Publish(Guid id)
        {
            var result = await _pageService.SetPublishedAsync(id, true);

            return Ok(ApiResult<AdminPageDto>.Success(result, "صفحه منتشر شد."));
        }

        [HttpPost("{id:guid}/unpublish")]
        public async Task<ActionResult<ApiResult<AdminPageDto>>> Unpublish(Guid id)
        {
            var result = await _pageService.SetPublishedAsync(id, false);

            return Ok(ApiResult<AdminPageDto>.Success(result, "صفحه از انتشار خارج شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _pageService.DeleteAsync(id);

            return Ok(ApiResult.Success("صفحه با موفقیت حذف شد."));
        }
    }
}
