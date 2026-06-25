using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers.Admin
{
    [ApiController]
    [Authorize(Policy = "AdminOnly")]
    [Route("api/admin/products")]
    public class AdminProductsController : ControllerBase
    {
        private readonly IAdminProductService _productService;

        public AdminProductsController(IAdminProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<AdminProductDto>>>> GetAll()
        {
            var result = await _productService.GetAllAsync();

            return Ok(ApiResult<List<AdminProductDto>>.Success(
                result,
                "لیست محصولات با موفقیت دریافت شد."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductDto>>> GetById(Guid id)
        {
            var result = await _productService.GetByIdAsync(id);

            return Ok(ApiResult<AdminProductDto>.Success(
                result,
                "محصول با موفقیت دریافت شد."));
        }

        [HttpPost]
        public async Task<ActionResult<ApiResult<AdminProductDto>>> Create(
            CreateProductRequestDto request)
        {
            var result = await _productService.CreateAsync(request);

            return Ok(ApiResult<AdminProductDto>.Success(
                result,
                "محصول با موفقیت ایجاد شد."));
        }

        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ApiResult<AdminProductDto>>> Update(
            Guid id,
            UpdateProductRequestDto request)
        {
            var result = await _productService.UpdateAsync(id, request);

            return Ok(ApiResult<AdminProductDto>.Success(
                result,
                "محصول با موفقیت ویرایش شد."));
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResult>> Delete(Guid id)
        {
            await _productService.DeleteAsync(id);

            return Ok(ApiResult.Success("محصول با موفقیت حذف شد."));
        }
    }
}