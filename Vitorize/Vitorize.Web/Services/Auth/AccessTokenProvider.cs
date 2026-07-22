using Microsoft.AspNetCore.Components.Authorization;

namespace Vitorize.Web.Services.Auth
{
    public class AccessTokenProvider : IAccessTokenProvider
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AccessTokenProvider(
            IServiceProvider serviceProvider,
            IHttpContextAccessor httpContextAccessor)
        {
            _serviceProvider = serviceProvider;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            // در رندر استاتیک/endpointها HttpContext در دسترس است؛
            // در این حالت توکن از کوکی یا claim خوانده می‌شود و هرگز سراغ
            // AuthenticationStateProvider نمی‌رویم (که خارج از مدار تعاملی ممکن است متوقف بماند).
            var httpContext = _httpContextAccessor.HttpContext;

            if (httpContext is not null)
            {
                // Select the token by the AUTHENTICATED SCHEME, not the request path. Framework
                // requests such as the Blazor circuit (/_blazor) are not under /admin, so a path check
                // would wrongly pick the customer token for an admin circuit when both cookies exist -
                // the admin API call then fails with 403 and the panel bounces to access-denied. The
                // resolved scheme (SmartScheme -> Admin/Customer) is the correct signal; fall back to
                // the path only for anonymous framework requests.
                var isAdminArea =
                    string.Equals(httpContext.User.Identity?.AuthenticationType,
                        VitorizeAuthSchemes.AdminScheme, StringComparison.Ordinal) ||
                    httpContext.Request.Path.StartsWithSegments("/admin");

                var areaCookie = isAdminArea
                    ? VitorizeAuthSchemes.AdminAccessTokenCookie
                    : VitorizeAuthSchemes.CustomerAccessTokenCookie;

                // Only ever the matching area's token: the fresh area cookie first, then the
                // authenticated principal's own claim. Never the other area's token (that caused the
                // admin panel to send the customer token and receive 403 when both cookies existed).
                var token =
                    httpContext.Request.Cookies[areaCookie] ??
                    httpContext.User.FindFirst("access_token")?.Value;

                return string.IsNullOrWhiteSpace(token) ? null : token;
            }

            // در مدار تعاملی (SignalR) HttpContext وجود ندارد؛ توکن از claimهای کاربر مدار خوانده می‌شود.
            var authStateProvider = _serviceProvider.GetService<AuthenticationStateProvider>();

            if (authStateProvider is not null)
            {
                try
                {
                    var state = await authStateProvider.GetAuthenticationStateAsync();
                    return state.User.FindFirst("access_token")?.Value;
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }
    }
}
