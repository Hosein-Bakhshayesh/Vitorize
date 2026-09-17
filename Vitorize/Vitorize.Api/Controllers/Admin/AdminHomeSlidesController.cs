using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.HomeSlides;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/home-slides")]
    public class AdminHomeSlidesController : ControllerBase
    {
        private readonly IAdminHomeSlideService _slideService;

        public AdminHomeSlidesController(IAdminHomeSlideService slideService)
        {
            _slideService = slideService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminHomeSlideDto>>>> GetAll()
        {
            var result = await _slideService.GetAllAsync();

            return Ok(ApiResult<List<AdminHomeSlideDto>>.Success(
                result,
                "لیست اسلایدها با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminHomeSlideDto>>> GetById(Guid id)
        {
            var result = await _slideService.GetByIdAsync(id);

            return Ok(ApiResult<AdminHomeSlideDto>.Success(
                result,
                "اسلاید با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminHomeSlideDto>>> Create(
            CreateHomeSlideRequestDto request)
        {
            var result = await _slideService.CreateAsync(request);

            return Ok(ApiResult<AdminHomeSlideDto>.Success(
                result,
                "اسلاید با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminHomeSlideDto>>> Update(
            Guid id,
            UpdateHomeSlideRequestDto request)
        {
            var result = await _slideService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminHomeSlideDto>.Success(
                result,
                "اسلاید با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _slideService.DeleteAsync(id);

            return Ok(ApiResult.Success("اسلاید با موفقیت حذف شد."));
        }
    }
}
