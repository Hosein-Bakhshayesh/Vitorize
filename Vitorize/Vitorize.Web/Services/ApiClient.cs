using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Components;
using Vitorize.Shared.Common;
using Vitorize.Shared.Logging;
using Vitorize.Web.Services.Auth;
using Vitorize.Web.Services.Cart;

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
        private readonly GuestCartIdentityProvider? _guestCartIdentity;
        private readonly SessionTokenRefreshCoordinator _refreshCoordinator;
        private readonly ITokenSessionPersistence _tokenSessionPersistence;
        private readonly IServiceProvider _serviceProvider;
        private readonly PrerenderApiState? _prerenderState;
        private readonly ILogger<ApiClient> _logger;
        private string? _expiredSessionScheme;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public ApiClient(
            HttpClient httpClient,
            IAccessTokenProvider tokenProvider,
            SessionTokenRefreshCoordinator refreshCoordinator,
            ITokenSessionPersistence tokenSessionPersistence,
            IServiceProvider serviceProvider,
            PrerenderApiState? prerenderState,
            ILogger<ApiClient> logger,
            GuestCartIdentityProvider? guestCartIdentity = null)
        {
            _httpClient = httpClient;
            _tokenProvider = tokenProvider;
            _guestCartIdentity = guestCartIdentity;
            _refreshCoordinator = refreshCoordinator;
            _tokenSessionPersistence = tokenSessionPersistence;
            _serviceProvider = serviceProvider;
            _prerenderState = prerenderState;
            _logger = logger;
        }

        public async Task<ApiResult<T>> GetAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            if (IsPublicPrerenderEndpoint(url) && _prerenderState?.TryTake<T>(url, out var persisted) == true && persisted is not null)
                return persisted;

            var result = await SendAsync<T>(HttpMethod.Get, url, null, cancellationToken: cancellationToken);
            if (IsPublicPrerenderEndpoint(url)) _prerenderState?.Remember(url, result);
            return result;
        }

        private static bool IsPublicPrerenderEndpoint(string url)
        {
            var path = url.TrimStart('/');
            return path.StartsWith("storefront", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("products", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("product-reviews", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("blog", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("pages", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("faqs", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("settings/public", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith("seo/", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<ApiResult<string>> GetRawTextAsync(string url, CancellationToken cancellationToken = default)
        {
            try
            {
                using var request = BuildRequest(HttpMethod.Get, url, null);
                await ApplyAuthAsync(request);
                ApplyCorrelation(request);
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(HttpMethod.Get, url, cancellationToken))
                {
                    using var retry = BuildRequest(HttpMethod.Get, url, null);
                    await ApplyAuthAsync(retry);
                    ApplyCorrelation(retry);
                    using var retriedResponse = await _httpClient.SendAsync(retry, cancellationToken);
                    HandleAuthFailure(url, retriedResponse.StatusCode);
                    var retriedContent = await retriedResponse.Content.ReadAsStringAsync(cancellationToken);
                    return retriedResponse.IsSuccessStatusCode ? ApiResult<string>.Success(retriedContent) : ApiResult<string>.Failure("دریافت فایل خروجی ناموفق بود.");
                }
                HandleAuthFailure(url, response.StatusCode);
                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                return response.IsSuccessStatusCode
                    ? ApiResult<string>.Success(content)
                    : ApiResult<string>.Failure("دریافت فایل خروجی ناموفق بود.");
            }
            catch (Exception ex)
            {
                LogTransportFailure(ex, HttpMethod.Get, url);
                return ApiResult<string>.Failure(ConnectionErrorMessage);
            }
        }

        public Task<ApiResult<T>> PostAsync<T>(string url, object? data = null, CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Post, url, data, cancellationToken: cancellationToken);

        public Task<ApiResult<T>> PostAsync<T>(
            string url,
            object? data,
            IReadOnlyDictionary<string, string> headers,
            CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Post, url, data, headers, cancellationToken);

        public Task<ApiResult<T>> PutAsync<T>(string url, object? data = null, CancellationToken cancellationToken = default) =>
            SendAsync<T>(HttpMethod.Put, url, data, cancellationToken: cancellationToken);

        public Task<ApiResult> PostAsync(string url, object? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(HttpMethod.Post, url, data, cancellationToken);

        public Task<ApiResult> PutAsync(string url, object? data = null, CancellationToken cancellationToken = default) =>
            SendAsync(HttpMethod.Put, url, data, cancellationToken);

        public Task<ApiResult> DeleteAsync(string url, CancellationToken cancellationToken = default) =>
            SendAsync(HttpMethod.Delete, url, null, cancellationToken);

        public async Task<ApiResult<T>> UploadAsync<T>(
            string url,
            Stream fileStream,
            string fileName,
            string contentType,
            string fieldName = "file",
            IReadOnlyDictionary<string, string>? fields = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (!await EnsureMutationAccessTokenAsync(url, cancellationToken))
                    return ApiResult<T>.Failure(ExpiredSessionMessage);
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                await ApplyAuthAsync(request);
                ApplyCorrelation(request);

                using var content = new MultipartFormDataContent();
                var fileContent = new StreamContent(fileStream);

                if (!string.IsNullOrWhiteSpace(contentType))
                    fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                content.Add(fileContent, fieldName, fileName);
                if (fields is not null)
                    foreach (var field in fields) content.Add(new StringContent(field.Value ?? string.Empty), field.Key);
                request.Content = content;

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                return Deserialize<ApiResult<T>>(json, response);
            }
            catch (Exception ex)
            {
                LogTransportFailure(ex, HttpMethod.Post, url);
                return ApiResult<T>.Failure(ConnectionErrorMessage);
            }
        }

        private async Task<ApiResult<T>> SendAsync<T>(
            HttpMethod method,
            string url,
            object? data,
            IReadOnlyDictionary<string, string>? headers = null,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (IsMutation(method) && !await EnsureMutationAccessTokenAsync(url, cancellationToken))
                    return AsAuthFailure(ApiResult<T>.Failure(ExpiredSessionMessage));
                using var request = BuildRequest(method, url, data);
                await ApplyAuthAsync(request);
                ApplyCorrelation(request);

                ApplyHeaders(request, headers);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized && await TryRefreshAsync(method, url, cancellationToken))
                {
                    using var retry = BuildRequest(method, url, data);
                    await ApplyAuthAsync(retry);
                    ApplyCorrelation(retry);
                    ApplyHeaders(retry, headers);
                    using var retriedResponse = await _httpClient.SendAsync(retry, cancellationToken);
                    HandleAuthFailure(url, retriedResponse.StatusCode);
                    return Deserialize<ApiResult<T>>(await retriedResponse.Content.ReadAsStringAsync(cancellationToken), retriedResponse);
                }
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                return Deserialize<ApiResult<T>>(json, response);
            }
            catch (Exception ex)
            {
                LogTransportFailure(ex, method, url);
                return ApiResult<T>.Failure(ConnectionErrorMessage);
            }
        }

        private async Task<ApiResult> SendAsync(
            HttpMethod method,
            string url,
            object? data,
            CancellationToken cancellationToken)
        {
            try
            {
                if (IsMutation(method) && !await EnsureMutationAccessTokenAsync(url, cancellationToken))
                    return AsAuthFailure(ApiResult.Failure(ExpiredSessionMessage));
                using var request = BuildRequest(method, url, data);
                await ApplyAuthAsync(request);
                ApplyCorrelation(request);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                HandleAuthFailure(url, response.StatusCode);
                var json = await response.Content.ReadAsStringAsync(cancellationToken);

                return Deserialize<ApiResult>(json, response);
            }
            catch (Exception ex)
            {
                LogTransportFailure(ex, method, url);
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
                return;
            }

            var guestToken = _guestCartIdentity?.GetToken();
            if (!string.IsNullOrWhiteSpace(guestToken))
                request.Headers.TryAddWithoutValidation("X-Vitorize-Guest-Cart", guestToken);
        }

        private async Task<bool> TryRefreshAsync(HttpMethod method, string url, CancellationToken cancellationToken)
        {
            // GET/HEAD are replay-safe. Never automatically repeat a mutation: a 401 may have
            // arrived after an upstream side effect, and duplicate commerce POSTs are unacceptable.
            if (method != HttpMethod.Get && method != HttpMethod.Head) return false;
            var path = url.TrimStart('/');
            if (path.StartsWith("auth/", StringComparison.OrdinalIgnoreCase)) return false;
            return await RefreshTokensAsync(cancellationToken);
        }

        private async Task<bool> EnsureMutationAccessTokenAsync(string url, CancellationToken cancellationToken)
        {
            var path = url.TrimStart('/');
            if (path.StartsWith("auth/", StringComparison.OrdinalIgnoreCase)) return true;
            var accessToken = await _tokenProvider.GetAccessTokenAsync();
            return !AccessTokenLifetime.RequiresRefresh(accessToken, DateTimeOffset.UtcNow)
                || await RefreshTokensAsync(cancellationToken);
        }

        /// <summary>
        /// Rotates the session's tokens. Returns whether the caller may retry its request.
        ///
        /// A session is ended here only when something authoritative says it is over: no refresh token
        /// at all, or the provider rejecting the one we hold. Everything else - a timeout, a recycling
        /// API, a 502 from the proxy, a circuit that cannot write cookies right now - leaves the
        /// session exactly as it was. Ending it on those was why customers were signed out mid-visit
        /// and had to clear their browser data to get back in.
        /// </summary>
        private async Task<bool> RefreshTokensAsync(CancellationToken cancellationToken)
        {
            var scheme = await _tokenProvider.GetSchemeAsync();
            var refresh = await _tokenProvider.GetRefreshTokenAsync();
            if (string.IsNullOrWhiteSpace(scheme) || string.IsNullOrWhiteSpace(refresh))
            {
                // Not a verdict from the provider - the local state simply is not available here,
                // which happens in a circuit whose claims have not been read yet. Ending the session
                // on that guess signed people out for no reason. Treat it as transient and let a later
                // request, with a real HttpContext, decide.
                _logger.LogWarning(
                    "No local scheme or refresh token available to rotate with; keeping the session. EventType={EventType}",
                    "TokenRefreshDeferred");
                return false;
            }

            var result = await _refreshCoordinator.RefreshAsync(scheme, refresh, cancellationToken);

            if (result.Outcome == RefreshOutcome.Transient)
            {
                // Keep the tokens we have. The next request retries, and a genuinely dead session
                // will be reported as Rejected then.
                _logger.LogWarning(
                    "Keeping the current session after a transient rotation failure. EventType={EventType}",
                    "TokenRefreshDeferred");
                return false;
            }

            if (!result.Success || result.AccessToken is null || result.RefreshToken is null)
            {
                await EndLocalSessionAsync(scheme);
                return false;
            }

            _tokenProvider.SetTokens(scheme, result.AccessToken, result.RefreshToken);

            if (await _tokenSessionPersistence.PersistAsync(scheme, result.AccessToken, result.RefreshToken, cancellationToken))
                return true;

            // The rotation succeeded, so the previous refresh token is already spent at the provider -
            // discarding the new pair here would strand the browser holding a revoked one, which is
            // the state that only clearing cookies could recover from. The new tokens are live in this
            // scope, so the request proceeds; and the pair is parked against the token the browser
            // still holds, so the next request that owns a real HTTP response adopts it and writes the
            // cookies. Without that handover the divergence outlived this scope and resurfaced as a
            // forced sign-out on the next page load.
            _serviceProvider.GetService<ITokenRotationHandoff>()
                ?.Remember(refresh!, scheme!, result.AccessToken, result.RefreshToken);
            _logger.LogWarning(
                "Rotated tokens could not be written to the browser yet; handed off for the next request. EventType={EventType}",
                "TokenRotationPersistenceDeferred");
            return true;
        }

        private static bool IsMutation(HttpMethod method) =>
            method == HttpMethod.Post || method == HttpMethod.Put || method == HttpMethod.Delete || method.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase);

        private async Task EndLocalSessionAsync(string? scheme)
        {
            _tokenProvider.ClearTokens();
            _expiredSessionScheme = scheme is VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme
                ? scheme
                : null;
            // The browser's cookies must go too, through whichever channel is available. A rendered
            // circuit has no HTTP response of its own, so this previously cleared only the in-memory
            // tokens and left a cookie holding a revoked refresh token behind - the state that could
            // only be recovered by clearing browser data.
            if (scheme is VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme)
                await _tokenSessionPersistence.EndSessionAsync(scheme, CancellationToken.None);
        }

        private static void ApplyHeaders(HttpRequestMessage request, IReadOnlyDictionary<string, string>? headers)
        {
            if (headers is null) return;
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        private static void ApplyCorrelation(HttpRequestMessage request)
        {
            var correlationId = CorrelationContext.Current ?? CorrelationIdPolicy.Generate();
            request.Headers.TryAddWithoutValidation(CorrelationIdPolicy.HeaderName, correlationId);
        }

        private void LogTransportFailure(Exception exception, HttpMethod method, string url)
        {
            var endpoint = SensitiveLogData.Sanitize(url.Split('?', 2)[0], 160);
            _logger.LogWarning(
                "API transport failed for {Method} {Endpoint}. ExceptionType={ExceptionType} EventType={EventType}",
                method.Method, endpoint, exception.GetType().Name, "ApiTransportFailed");
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

                if (statusCode == HttpStatusCode.Unauthorized &&
                    _expiredSessionScheme is VitorizeAuthSchemes.AdminScheme or VitorizeAuthSchemes.CustomerScheme)
                {
                    var area = _expiredSessionScheme == VitorizeAuthSchemes.AdminScheme ? "admin" : "customer";
                    _expiredSessionScheme = null;
                    navigation.NavigateTo(
                        $"auth/session-expired?area={area}&returnUrl={Uri.EscapeDataString(currentPath)}",
                        forceLoad: true,
                        replace: true);
                    return;
                }

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

        private static T AuthFailure<T>(string message)
        {
            var failure = CreateFailure<T>(message);
            if (failure is ApiResult result) result.RequiresAuthentication = true;
            return failure;
        }

        private static TResult AsAuthFailure<TResult>(TResult result) where TResult : ApiResult
        {
            result.RequiresAuthentication = true;
            return result;
        }

        private const string ConnectionErrorMessage =
            "امکان برقراری ارتباط با سرور وجود ندارد. لطفاً اتصال خود را بررسی کرده و دوباره تلاش کنید.";

        private const string ExpiredSessionMessage =
            "نشست شما منقضی شده است. لطفاً دوباره وارد شوید.";

        private static T Deserialize<T>(string json, HttpResponseMessage response)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized ||
                    response.StatusCode == HttpStatusCode.Forbidden)
                    return AuthFailure<T>("دسترسی شما به این بخش مجاز نیست یا نشست شما منقضی شده است.");

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
                    return AuthFailure<T>("دسترسی شما به این بخش مجاز نیست یا نشست شما منقضی شده است.");

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
