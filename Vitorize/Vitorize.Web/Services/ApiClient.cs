using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Vitorize.Shared.Common;
using Vitorize.Web.Services.Auth;

namespace Vitorize.Web.Services
{
    /// <summary>
    /// کلاینت ارتباط با APIهای بک‌اند ویتورایز.
    /// آدرس پایه شامل /api/ است؛ پس مسیرها به‌صورت admin/... ارسال می‌شوند.
    /// تمام خطاها به پیام فارسی کاربرپسند تبدیل می‌شوند.
    /// </summary>
    public class ApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly IAccessTokenProvider _tokenProvider;
        private readonly IServiceProvider _serviceProvider;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient(
            HttpClient httpClient,
            IAccessTokenProvider tokenProvider,
            IServiceProvider serviceProvider)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;
            _serviceProvider = serviceProvider;
        }

        public Task<ApiResult<T>> GetAsync<T>(string url) =>
            SendAsync<T>(HttpMethod.Get, url, null);

        public Task<ApiResult<T>> PostAsync<T>(string url, object? data = null) =>
            SendAsync<T>(HttpMethod.Post, url, data);

        public Task<ApiResult<T>> PostAsync<T>(
            string url,
            object? data,
            IReadOnlyDictionary<string, string> headers) =>
            SendAsync<T>(HttpMethod.Post, url, data, headers);

        public Task<ApiResult<T>> PutAsync<T>(string url, object? data = null) =>
            SendAsync<T>(HttpMethod.Put, url, data);

        public Task<ApiResult> PostAsync(string url, object? data = null) =>
            SendAsync(HttpMethod.Post, url, data);

        public Task<ApiResult> PutAsync(string url, object? data = null) =>
            SendAsync(HttpMethod.Put, url, data);

        public Task<ApiResult> DeleteAsync(string url) =>
            SendAsync(HttpMethod.Delete, url, null);

        public async Task<ApiResult<T>> UploadAsync<T>(
            string url,
            Stream fileStream,
            string fileName,
            string contentType,
            string fieldName = "file")
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                await ApplyAuthAsync(request);

                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(fileStream);

                if (!string.IsNullOrWhiteSpace(contentType))
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                content.Add(fileContent, fieldName, fileName);
                request.Content = content;

                using var response = await _httpClient.SendAsync(request);
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync();

                return Deserialize<ApiResult<T>>(json, response);
            }
            catch (Exception)
            {
                return ApiResult<T>.Failure(ConnectionErrorMessage);
            }
        }

        private async Task<ApiResult<T>> SendAsync<T>(
            HttpMethod method,
            string url,
            object? data,
            IReadOnlyDictionary<string, string>? headers = null)
        {
            try
            {
                using var request = BuildRequest(method, url, data);
                await ApplyAuthAsync(request);

                if (headers is not null)
                {
                    foreach (var header in headers)
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                }

                using var response = await _httpClient.SendAsync(request);
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync();

                return Deserialize<ApiResult<T>>(json, response);
            }
            catch (Exception)
            {
                return ApiResult<T>.Failure(ConnectionErrorMessage);
            }
        }

        private async Task<ApiResult> SendAsync(
            HttpMethod method,
            string url,
            object? data)
        {
            try
            {
                using var request = BuildRequest(method, url, data);
                await ApplyAuthAsync(request);

                using var response = await _httpClient.SendAsync(request);
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync();

                return Deserialize<ApiResult>(json, response);
            }
            catch (Exception)
            {
                return ApiResult.Failure(ConnectionErrorMessage);
            }
        }

        private static HttpRequestMessage BuildRequest(
            HttpMethod method,
            string url,
            object? data)
        {
            var request = new HttpRequestMessage(method, url);

            if (data != null && method != HttpMethod.Get && method != HttpMethod.Delete)
            {
                var json = JsonSerializer.Serialize(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            }

            return request;
        }

        private async Task ApplyAuthAsync(HttpRequestMessage request)
        {
            var token = await _tokenProvider.GetAccessTokenAsync();

            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", token);
            }
        }

        /// <summary>
        /// در صورت دریافت ۴۰۱/۴۰۳ از APIهای ادمین، کاربر به صفحه‌ی مناسب هدایت می‌شود.
        /// فقط در مدار تعاملی (که NavigationManager مقداردهی شده) اجرا می‌شود.
        /// </summary>
        private void HandleAuthFailure(string url, HttpStatusCode statusCode)
        {
            if (statusCode != HttpStatusCode.Unauthorized &&
                statusCode != HttpStatusCode.Forbidden)
                return;

            // فراخوانی‌های مربوط به خود فرایند ورود را نادیده می‌گیریم.
            var apiPath = url.TrimStart('/');
            if (apiPath.StartsWith("auth", StringComparison.OrdinalIgnoreCase))
                return;

            var navigation = _serviceProvider.GetService<NavigationManager>();
            if (navigation is null)
                return;

            try
            {
                var currentPath = "/" + navigation.ToBaseRelativePath(navigation.Uri);
                var lower = currentPath.ToLowerInvariant();

                var isAdminArea = lower.StartsWith("/admin");

                // فقط در ناحیه‌های محافظت‌شده هدایت خودکار انجام می‌شود تا
                // صفحات عمومی هنگام فراخوانی‌های اختیاری به ورود پرتاب نشوند.
                var isProtectedArea =
                    isAdminArea ||
                    lower.StartsWith("/customer") ||
                    lower.StartsWith("/cart") ||
                    lower.StartsWith("/checkout");

                if (!isProtectedArea)
                    return;

                if (statusCode == HttpStatusCode.Forbidden)
                {
                    navigation.NavigateTo(
                        isAdminArea ? "admin/access-denied" : "access-denied",
                        forceLoad: false, replace: true);
                    return;
                }

                // ۴۰۱: نشست منقضی شده؛ بازگشت به صفحه ورود با حفظ مسیر فعلی.
                var loginPath = isAdminArea ? "admin/login" : "login";
                navigation.NavigateTo(
                    $"{loginPath}?returnUrl={Uri.EscapeDataString(currentPath)}",
                    forceLoad: true,
                    replace: true);
            }
            catch
            {
                // خارج از مدار تعاملی یا در حین رندر اولیه، هدایت انجام نمی‌شود.
            }
        }

        private const string ConnectionErrorMessage =
            "امکان برقراری ارتباط با سرور وجود ندارد. لطفاً اتصال خود را بررسی کرده و دوباره تلاش کنید.";

        private static T Deserialize<T>(string json, HttpResponseMessage response)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                    return CreateFailure<T>("دسترسی شما به این بخش مجاز نیست یا نشست شما منقضی شده است.");

                return CreateFailure<T>("پاسخی از سرور دریافت نشد. لطفاً دوباره تلاش کنید.");
            }

            try
            {
                var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

                if (result == null)
                    return CreateFailure<T>("پاسخ سرور قابل پردازش نیست. لطفاً دوباره تلاش کنید.");

                return result;
            }
            catch
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                    return CreateFailure<T>("دسترسی شما به این بخش مجاز نیست یا نشست شما منقضی شده است.");

                return CreateFailure<T>("پاسخ سرور قابل پردازش نیست. لطفاً دوباره تلاش کنید.");
            }
        }

        private static T CreateFailure<T>(string message)
        {
            if (typeof(T) == typeof(ApiResult))
                return (T)(object)ApiResult.Failure(message);

            if (typeof(T).IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ApiResult<>))
            {
                var dataType = typeof(T).GetGenericArguments()[0];
                var apiResultType = typeof(ApiResult<>).MakeGenericType(dataType);

                var failureMethod = apiResultType.GetMethod(
                    "Failure",
                    new[] { typeof(string), typeof(List<string>) });

                if (failureMethod != null)
                {
                    return (T)failureMethod.Invoke(null, new object?[] { message, null })!;
                }
            }

            return Activator.CreateInstance<T>();
        }
    }
}
