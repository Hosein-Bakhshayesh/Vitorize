using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Brands;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/brands")]
    public class AdminBrandsController : ControllerBase
    {
        private readonly IAdminBrandService _brandService;

        public AdminBrandsController(IAdminBrandService brandService)
        {
            _brandService = brandService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminBrandDto>>>> GetAll()
        {
            var result = await _brandService.GetAllAsync();

            return Ok(ApiResult<List<AdminBrandDto>>.Success(
                result,
                "لیست برندها با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBrandDto>>> GetById(Guid id)
        {
            var result = await _brandService.GetByIdAsync(id);

            return Ok(ApiResult<AdminBrandDto>.Success(
                result,
                "برند با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminBrandDto>>> Create(
            CreateBrandRequestDto request)
        {
            var result = await _brandService.CreateAsync(request);

            return Ok(ApiResult<AdminBrandDto>.Success(
                result,
                "برند با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminBrandDto>>> Update(
            Guid id,
            UpdateBrandRequestDto request)
        {
            var result = await _brandService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminBrandDto>.Success(
                result,
                "برند با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _brandService.DeleteAsync(id);

            return Ok(ApiResult.Success("برند با موفقیت حذف شد."));
        }
    }
}