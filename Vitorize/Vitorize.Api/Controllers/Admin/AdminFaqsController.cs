using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Content;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "SettingsManage")]
    [Route("api/admin/faqs")]
    public class AdminFaqsController : ControllerBase
    {
        private readonly IAdminFaqService _faqService;

        public AdminFaqsController(IAdminFaqService faqService)
        {
            _faqService = faqService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminFaqDto>>>> GetAll()
        {
            var result = await _faqService.GetAllAsync();

            return Ok(ApiResult<List<AdminFaqDto>>.Success(
                result,
                "لیست سوالات متداول با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminFaqDto>>> GetById(Guid id)
        {
            var result = await _faqService.GetByIdAsync(id);

            return Ok(ApiResult<AdminFaqDto>.Success(result, "پرسش با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminFaqDto>>> Create(CreateFaqRequestDto request)
        {
            var result = await _faqService.CreateAsync(request);

            return Ok(ApiResult<AdminFaqDto>.Success(result, "پرسش با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminFaqDto>>> Update(Guid id, UpdateFaqRequestDto request)
        {
            var result = await _faqService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminFaqDto>.Success(result, "پرسش با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _faqService.DeleteAsync(id);

            return Ok(ApiResult.Success("پرسش با موفقیت حذف شد."));
        }
    }
}
