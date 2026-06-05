using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Vitorize.Shared.Common;

namespace Vitorize.Web.Services
{
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient(
            HttpClient httpClient,
            IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<ApiResult<T>> GetAsync<T>(string url)
        {
            AddBearerToken();

            var response = await _httpClient.GetAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json);
        }

        public async Task<ApiResult<T>> PostAsync<T>(string url, object? data = null)
        {
            AddBearerToken();

            var content = CreateJsonContent(data);
            var response = await _httpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json);
        }

        public async Task<ApiResult<T>> PutAsync<T>(string url, object? data = null)
        {
            AddBearerToken();

            var content = CreateJsonContent(data);
            var response = await _httpClient.PutAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json);
        }

        public async Task<ApiResult> DeleteAsync(string url)
        {
            AddBearerToken();

            var response = await _httpClient.DeleteAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult>(json);
        }

        private void AddBearerToken()
        {
            var token = _httpContextAccessor.HttpContext?
                .Request
                .Cookies["Vitorize.AccessToken"];

            if (!string.IsNullOrWhiteSpace(token))
            {
                _httpClient.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        private static StringContent CreateJsonContent(object? data)
        {
            var json = JsonSerializer.Serialize(data ?? new { });
            return new StringContent(json, Encoding.UTF8, "application/json");
        }

        private static T Deserialize<T>(string json)
        {
            var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

            if (result == null)
                throw new Exception("خطا در خواندن پاسخ API.");

            return result;
        }
    }
}