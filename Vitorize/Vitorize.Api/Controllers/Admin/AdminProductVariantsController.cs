using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    public class AdminProductVariantsController : ControllerBase
    {
        private readonly IAdminProductVariantService _variantService;

        public AdminProductVariantsController(IAdminProductVariantService variantService)
        {
            _variantService = variantService;
        }

        [HttpGet("products/{productId:guid}/variants")]
        public async Task<ActionResult<ApiResult<List<AdminProductVariantDto>>>> GetByProductId(
            Guid productId)
        {
            var result = await _variantService.GetByProductIdAsync(productId);

            return Ok(ApiResult<List<AdminProductVariantDto>>.Success(
                result,
                "لیست تنوع‌های محصول با موفقیت دریافت شد."));
        }

        [HttpGet("product-variants/{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductVariantDto>>> GetById(Guid id)
        {
            var result = await _variantService.GetByIdAsync(id);

            return Ok(ApiResult<AdminProductVariantDto>.Success(
                result,
                "تنوع محصول با موفقیت دریافت شد."));
        }

        [HttpPost("products/{productId:guid}/variants")]
        public async Task<ActionResult<ApiResult<AdminProductVariantDto>>> Create(
            Guid productId,
            CreateProductVariantRequestDto request)
        {
            var result = await _variantService.CreateAsync(productId, request);

            return Ok(ApiResult<AdminProductVariantDto>.Success(
                result,
                "تنوع محصول با موفقیت ایجاد شد."));
        }

        [HttpPut("product-variants/{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductVariantDto>>> Update(
            Guid id,
            UpdateProductVariantRequestDto request)
        {
            var result = await _variantService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminProductVariantDto>.Success(
                result,
                "تنوع محصول با موفقیت ویرایش شد."));
        }

        [HttpDelete("product-variants/{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _variantService.DeleteAsync(id);

            return Ok(ApiResult.Success("تنوع محصول با موفقیت حذف شد."));
        }
    }
}