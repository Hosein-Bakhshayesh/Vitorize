using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.ProductImages;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin")]
    public class AdminProductImagesController : ControllerBase
    {
        private readonly IAdminProductImageService _productImageService;

        public AdminProductImagesController(
            IAdminProductImageService productImageService)
        {
            _productImageService = productImageService;
        }

        [HttpGet("products/{productId:guid}/images")]
        public async Task<ActionResult<ApiResult<List<AdminProductImageDto>>>> GetByProductId(
            Guid productId)
        {
            var result = await _productImageService.GetByProductIdAsync(productId);

            return Ok(ApiResult<List<AdminProductImageDto>>.Success(
                result,
                "تصاویر محصول با موفقیت دریافت شدند."));
        }

        [HttpPost("products/{productId:guid}/images")]
        public async Task<ActionResult<ApiResult<AdminProductImageDto>>> Create(
            Guid productId,
            CreateProductImageRequestDto request)
        {
            var result = await _productImageService.CreateAsync(
                productId,
                request);

            return Ok(ApiResult<AdminProductImageDto>.Success(
                result,
                "تصویر محصول با موفقیت ثبت شد."));
        }

        [HttpPut("product-images/{imageId:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductImageDto>>> Update(
            Guid imageId,
            UpdateProductImageRequestDto request)
        {
            var result = await _productImageService.UpdateAsync(
                imageId,
                request);

            return Ok(ApiResult<AdminProductImageDto>.Success(
                result,
                "تصویر محصول با موفقیت ویرایش شد."));
        }

        [HttpPost("product-images/{imageId:guid}/set-thumbnail")]
        public async Task<ActionResult<ApiResult>> SetAsThumbnail(Guid imageId)
        {
            await _productImageService.SetAsThumbnailAsync(imageId);

            return Ok(ApiResult.Success(
                "تصویر اصلی محصول با موفقیت تغییر کرد."));
        }

        [HttpDelete("product-images/{imageId:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid imageId)
        {
            await _productImageService.DeleteAsync(imageId);

            return Ok(ApiResult.Success(
                "تصویر محصول با موفقیت حذف شد."));
        }
    }
}