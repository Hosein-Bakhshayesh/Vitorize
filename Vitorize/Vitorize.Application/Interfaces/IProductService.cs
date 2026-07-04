using Vitorize.Application.DTOs.Products;
using Vitorize.Shared.Common;

namespace Vitorize.Application.Interfaces
{
    public interface IProductService
    {
        Task<PagedResult<ProductListItemDto>> GetProductsAsync(ProductFilterDto filter);

        Task<ProductDetailDto> GetProductByIdAsync(Guid id);

        Task<ProductDetailDto> GetProductBySlugAsync(string slug);

        Task<List<ProductListItemDto>> GetFeaturedProductsAsync(int count = 10);

        Task<List<ProductListItemDto>> GetRelatedProductsAsync(Guid productId, int count = 8);

        Task<List<ProductLookupDto>> GetCategoriesAsync();

        Task<List<ProductLookupDto>> GetBrandsAsync();
    }
}
