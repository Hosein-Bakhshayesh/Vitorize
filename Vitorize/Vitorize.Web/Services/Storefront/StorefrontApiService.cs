using System.Text;
using Vitorize.Web.Models.Storefront;

namespace Vitorize.Web.Services.Storefront
{
    public class StorefrontApiService : IStorefrontApiService
    {
        private readonly ApiClient _api;
        private readonly ILogger<StorefrontApiService> _logger;

        public StorefrontApiService(
            ApiClient api,
            ILogger<StorefrontApiService> logger)
        {
            _api = api;
            _logger = logger;
        }

        public async Task<List<StoreProductCardModel>> GetFeaturedProductsAsync(int count = 8)
        {
            var result = await SafeGet<List<StoreProductCardModel>>(
                $"products/featured?count={count}");

            return result ?? new List<StoreProductCardModel>();
        }

        public async Task<List<StoreProductCardModel>> GetProductsAsync(StoreProductFilterModel filter)
        {
            var query = BuildQuery(new Dictionary<string, string?>
            {
                ["Search"] = filter.Search,
                ["CategoryId"] = filter.CategoryId?.ToString(),
                ["BrandId"] = filter.BrandId?.ToString(),
                ["IsFeatured"] = filter.IsFeatured?.ToString(),
                ["Page"] = filter.Page.ToString(),
                ["PageSize"] = filter.PageSize.ToString()
            });

            var result = await SafeGet<List<StoreProductCardModel>>(
                $"products{query}");

            return result ?? new List<StoreProductCardModel>();
        }

        public async Task<StoreProductDetailsModel?> GetProductBySlugAsync(string slug)
        {
            return await SafeGet<StoreProductDetailsModel>(
                $"products/slug/{Uri.EscapeDataString(slug)}");
        }

        public async Task<StoreProductDetailsModel?> GetProductByIdAsync(Guid id)
        {
            return await SafeGet<StoreProductDetailsModel>(
                $"products/{id}");
        }

        public async Task<List<StoreProductLookupModel>> GetCategoriesAsync()
        {
            var result = await SafeGet<List<StoreProductLookupModel>>(
                "products/categories");

            return result ?? new List<StoreProductLookupModel>();
        }

        public async Task<List<StoreProductLookupModel>> GetBrandsAsync()
        {
            var result = await SafeGet<List<StoreProductLookupModel>>(
                "products/brands");

            return result ?? new List<StoreProductLookupModel>();
        }

        public async Task<StoreCartModel?> GetCartAsync()
        {
            return await SafeGet<StoreCartModel>("cart");
        }

        public async Task<StoreCartModel?> AddToCartAsync(StoreAddToCartRequestModel request)
        {
            return await SafePost<StoreCartModel>("cart/items", request);
        }

        public async Task<StoreCartModel?> UpdateCartItemAsync(Guid cartItemId, int quantity)
        {
            return await SafePut<StoreCartModel>(
                $"cart/items/{cartItemId}",
                new StoreUpdateCartItemRequestModel
                {
                    Quantity = quantity
                });
        }

        public async Task<StoreCartModel?> RemoveCartItemAsync(Guid cartItemId)
        {
            await SafeDelete($"cart/items/{cartItemId}");
            return await GetCartAsync();
        }

        public async Task<StoreCheckoutResultModel?> CheckoutAsync(StoreCheckoutRequestModel request)
        {
            return await SafePost<StoreCheckoutResultModel>("checkout", request);
        }

        public async Task<StorePaymentStartResultModel?> StartPaymentAsync(Guid orderId)
        {
            return await SafePost<StorePaymentStartResultModel>(
                $"payments/start/{orderId}",
                new { });
        }

        public async Task<List<StoreOrderModel>> GetMyOrdersAsync()
        {
            var result = await SafeGet<List<StoreOrderModel>>("orders");
            return result ?? new List<StoreOrderModel>();
        }

        public async Task<StoreOrderModel?> GetMyOrderDetailsAsync(Guid orderId)
        {
            return await SafeGet<StoreOrderModel>($"orders/{orderId}");
        }

        private async Task<T?> SafeGet<T>(string url)
        {
            try
            {
                var result = await _api.GetAsync<T>(url);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning(
                        "API GET failed. Url: {Url}, Message: {Message}",
                        url,
                        result.Message);

                    return default;
                }

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API GET exception. Url: {Url}", url);
                return default;
            }
        }

        private async Task<T?> SafePost<T>(string url, object data)
        {
            try
            {
                var result = await _api.PostAsync<T>(url, data);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning(
                        "API POST failed. Url: {Url}, Message: {Message}",
                        url,
                        result.Message);

                    return default;
                }

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API POST exception. Url: {Url}", url);
                return default;
            }
        }

        private async Task<T?> SafePut<T>(string url, object data)
        {
            try
            {
                var result = await _api.PutAsync<T>(url, data);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning(
                        "API PUT failed. Url: {Url}, Message: {Message}",
                        url,
                        result.Message);

                    return default;
                }

                return result.Data;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API PUT exception. Url: {Url}", url);
                return default;
            }
        }

        private async Task<bool> SafeDelete(string url)
        {
            try
            {
                var result = await _api.DeleteAsync(url);

                if (!result.IsSuccess)
                {
                    _logger.LogWarning(
                        "API DELETE failed. Url: {Url}, Message: {Message}",
                        url,
                        result.Message);

                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "API DELETE exception. Url: {Url}", url);
                return false;
            }
        }

        private static string BuildQuery(Dictionary<string, string?> values)
        {
            var validValues = values
                .Where(x => !string.IsNullOrWhiteSpace(x.Value))
                .ToList();

            if (!validValues.Any())
                return string.Empty;

            var query = new StringBuilder("?");

            query.Append(string.Join(
                "&",
                validValues.Select(x =>
                    $"{Uri.EscapeDataString(x.Key)}={Uri.EscapeDataString(x.Value!)}")));

            return query.ToString();
        }
        public async Task<StoreCurrentUserModel?> GetCurrentUserAsync()
        {
            return await SafeGet<StoreCurrentUserModel>("auth/me");
        }

        public async Task<StorePaymentVerifyResultModel?> VerifyMockPaymentAsync(Guid paymentId)
        {
            return await SafePost<StorePaymentVerifyResultModel>(
                $"payments/mock/verify/{paymentId}",
                new { });
        }
        public async Task ClearCartAsync()
        {
            await SafeDelete("cart/clear");
        }
    }
}