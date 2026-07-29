using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Admin;
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

        [HttpGet("paged")]
        public async Task<ActionResult<ApiResult<PagedResult<AdminProductDto>>>> GetPaged([FromQuery] AdminProductFilterDto filter, CancellationToken cancellationToken)
        {
            var result = await _productService.GetPagedAsync(filter, cancellationToken);
            return Ok(ApiResult<PagedResult<AdminProductDto>>.Success(result, "فهرست صفحه‌بندی‌شده محصولات دریافت شد."));
        }

        [HttpPost("export-selection")]
        public async Task<ActionResult<ApiResult<List<AdminProductDto>>>> ExportSelection(
            SelectedRowsRequestDto request, CancellationToken cancellationToken)
        {
            var result = await _productService.GetSelectedForExportAsync(request.Ids, cancellationToken);
            return Ok(ApiResult<List<AdminProductDto>>.Success(result, "محصولات انتخاب‌شده برای خروجی تأیید شدند."));
        }

        [HttpGet("lookup")]
        public async Task<ActionResult<ApiResult<List<AdminProductLookupDto>>>> GetLookup([FromQuery] string? search, [FromQuery] Guid? selectedId, CancellationToken cancellationToken)
        {
            var result = await _productService.GetLookupAsync(search, selectedId, cancellationToken);
            return Ok(ApiResult<List<AdminProductLookupDto>>.Success(result, "فهرست محدود محصولات دریافت شد."));
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
