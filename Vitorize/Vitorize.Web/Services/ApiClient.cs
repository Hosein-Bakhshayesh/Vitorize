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

            return Deserialize<ApiResult<T>>(json, response);
        }

        public async Task<ApiResult<T>> PostAsync<T>(string url, object? data = null)
        {
            AddBearerToken();

            var content = CreateJsonContent(data);
            var response = await _httpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json, response);
        }

        public async Task<ApiResult<T>> PutAsync<T>(string url, object? data = null)
        {
            AddBearerToken();

            var content = CreateJsonContent(data);
            var response = await _httpClient.PutAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json, response);
        }

        public async Task<ApiResult> DeleteAsync(string url)
        {
            AddBearerToken();

            var response = await _httpClient.DeleteAsync(url);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult>(json, response);
        }

        public async Task<ApiResult<T>> UploadFileAsync<T>(
            string url,
            IFormFile file,
            string fieldName = "file")
        {
            AddBearerToken();

            using var content = new MultipartFormDataContent();

            await using var stream = file.OpenReadStream();

            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new MediaTypeHeaderValue(file.ContentType);

            content.Add(fileContent, fieldName, file.FileName);

            var response = await _httpClient.PostAsync(url, content);
            var json = await response.Content.ReadAsStringAsync();

            return Deserialize<ApiResult<T>>(json, response);
        }

        private void AddBearerToken()
        {
            var httpContext = _httpContextAccessor.HttpContext;

            var token = httpContext?
                .Request
                .Cookies["Vitorize.AccessToken"];

            if (string.IsNullOrWhiteSpace(token))
            {
                token = httpContext?
                    .User
                    .FindFirst("access_token")
                    ?.Value;
            }

            _httpClient.DefaultRequestHeaders.Authorization = null;

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

        private static T Deserialize<T>(
            string json,
            HttpResponseMessage response)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return CreateFailure<T>(
                    $"پاسخ API خالی است. StatusCode: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            try
            {
                var result = JsonSerializer.Deserialize<T>(json, JsonOptions);

                if (result == null)
                {
                    return CreateFailure<T>(
                        $"پاسخ API قابل خواندن نیست. StatusCode: {(int)response.StatusCode} - {response.ReasonPhrase}");
                }

                return result;
            }
            catch
            {
                return CreateFailure<T>(
                    $"پاسخ API معتبر نیست. StatusCode: {(int)response.StatusCode} - {response.ReasonPhrase}");
            }
        }

        private static T CreateFailure<T>(string message)
        {
            if (typeof(T) == typeof(ApiResult))
            {
                return (T)(object)ApiResult.Failure(message);
            }

            if (typeof(T).IsGenericType &&
                typeof(T).GetGenericTypeDefinition() == typeof(ApiResult<>))
            {
                var dataType = typeof(T).GetGenericArguments()[0];

                var apiResultType = typeof(ApiResult<>).MakeGenericType(dataType);

                var failureMethod = apiResultType.GetMethod(
                    "Failure",
                    new[] { typeof(string), typeof(List<string>) });

                if (failureMethod == null)
                    throw new Exception(message);

                return (T)failureMethod.Invoke(
                    null,
                    new object?[] { message, null })!;
            }

            throw new Exception(message);
        }
    }
}