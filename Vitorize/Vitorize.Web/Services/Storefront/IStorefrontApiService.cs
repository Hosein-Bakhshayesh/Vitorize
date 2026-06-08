using Vitorize.Web.Models.Storefront;

namespace Vitorize.Web.Services.Storefront
{
    public interface IStorefrontApiService
    {
        Task<List<StoreProductCardModel>> GetFeaturedProductsAsync(int count = 8);
        Task<List<StoreProductCardModel>> GetProductsAsync(StoreProductFilterModel filter);
        Task<StoreProductDetailsModel?> GetProductBySlugAsync(string slug);
        Task<StoreProductDetailsModel?> GetProductByIdAsync(Guid id);
        Task<List<StoreProductLookupModel>> GetCategoriesAsync();
        Task<List<StoreProductLookupModel>> GetBrandsAsync();
        Task<StoreCartModel?> GetCartAsync();
        Task<StoreCartModel?> AddToCartAsync(StoreAddToCartRequestModel request);
        Task<StoreCartModel?> UpdateCartItemAsync(Guid cartItemId, int quantity);
        Task<StoreCartModel?> RemoveCartItemAsync(Guid cartItemId);
        Task<StoreCheckoutResultModel?> CheckoutAsync(StoreCheckoutRequestModel request);
        Task<StorePaymentStartResultModel?> StartPaymentAsync(Guid orderId);
        Task<List<StoreOrderModel>> GetMyOrdersAsync();
        Task<StoreOrderModel?> GetMyOrderDetailsAsync(Guid orderId);
        Task<StoreCurrentUserModel?> GetCurrentUserAsync();
        Task<StorePaymentVerifyResultModel?> VerifyMockPaymentAsync(Guid paymentId);
        Task ClearCartAsync();
    }
}
