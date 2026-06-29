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
                var cookieToken =
                    httpContext.Request.Cookies[VitorizeAuthSchemes.AdminAccessTokenCookie];

                if (!string.IsNullOrWhiteSpace(cookieToken))
                    return cookieToken;

                return httpContext.User.FindFirst("access_token")?.Value;
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
