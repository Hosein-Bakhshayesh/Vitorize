using Microsoft.AspNetCore.Mvc;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Common;

namespace Vitorize.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;

        public ProductsController(IProductService productService)
        {
            _productService = productService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResult<List<ProductListItemDto>>>> GetProducts(
            [FromQuery] ProductFilterDto filter)
        {
            var result = await _productService.GetProductsAsync(filter);

            return Ok(ApiResult<List<ProductListItemDto>>.Success(
                result,
                "لیست محصولات با موفقیت دریافت شد."));
        }

        [HttpGet("featured")]
        public async Task<ActionResult<ApiResult<List<ProductListItemDto>>>> GetFeaturedProducts(
            [FromQuery] int count = 10)
        {
            var result = await _productService.GetFeaturedProductsAsync(count);

            return Ok(ApiResult<List<ProductListItemDto>>.Success(
                result,
                "محصولات ویژه با موفقیت دریافت شدند."));
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResult<ProductDetailDto>>> GetProductById(Guid id)
        {
            var result = await _productService.GetProductByIdAsync(id);

            return Ok(ApiResult<ProductDetailDto>.Success(
                result,
                "جزئیات محصول با موفقیت دریافت شد."));
        }

        [HttpGet("slug/{slug}")]
        public async Task<ActionResult<ApiResult<ProductDetailDto>>> GetProductBySlug(string slug)
        {
            var result = await _productService.GetProductBySlugAsync(slug);

            return Ok(ApiResult<ProductDetailDto>.Success(
                result,
                "جزئیات محصول با موفقیت دریافت شد."));
        }

        [HttpGet("categories")]
        public async Task<ActionResult<ApiResult<List<ProductLookupDto>>>> GetCategories()
        {
            var result = await _productService.GetCategoriesAsync();

            return Ok(ApiResult<List<ProductLookupDto>>.Success(
                result,
                "دسته‌بندی‌ها با موفقیت دریافت شدند."));
        }

        [HttpGet("brands")]
        public async Task<ActionResult<ApiResult<List<ProductLookupDto>>>> GetBrands()
        {
            var result = await _productService.GetBrandsAsync();

            return Ok(ApiResult<List<ProductLookupDto>>.Success(
                result,
                "برندها با موفقیت دریافت شدند."));
        }
    }
}