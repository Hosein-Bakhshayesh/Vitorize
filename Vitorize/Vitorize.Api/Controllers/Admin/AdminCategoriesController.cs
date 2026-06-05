using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Categories;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/categories")]
    public class AdminCategoriesController : ControllerBase
    {
        private readonly IAdminCategoryService _categoryService;

        public AdminCategoriesController(IAdminCategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminCategoryDto>>>> GetAll()
        {
            var result = await _categoryService.GetAllAsync();

            return Ok(ApiResult<List<AdminCategoryDto>>.Success(
                result,
                "لیست دسته‌بندی‌ها با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminCategoryDto>>> GetById(Guid id)
        {
            var result = await _categoryService.GetByIdAsync(id);

            return Ok(ApiResult<AdminCategoryDto>.Success(
                result,
                "دسته‌بندی با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminCategoryDto>>> Create(
            CreateCategoryRequestDto request)
        {
            var result = await _categoryService.CreateAsync(request);

            return Ok(ApiResult<AdminCategoryDto>.Success(
                result,
                "دسته‌بندی با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminCategoryDto>>> Update(
            Guid id,
            UpdateCategoryRequestDto request)
        {
            var result = await _categoryService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminCategoryDto>.Success(
                result,
                "دسته‌بندی با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _categoryService.DeleteAsync(id);

            return Ok(ApiResult.Success("دسته‌بندی با موفقیت حذف شد."));
        }
    }
}