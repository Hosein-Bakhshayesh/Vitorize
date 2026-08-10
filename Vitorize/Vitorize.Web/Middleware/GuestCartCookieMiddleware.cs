using Vitorize.Application.Cart;
using Vitorize.Web.Services.Cart;

namespace Vitorize.Web.Middleware;

/// <summary>Provisions an opaque guest-cart capability before a storefront response starts.</summary>
public sealed class GuestCartCookieMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GuestCartCookieMiddleware> _logger;

    public GuestCartCookieMiddleware(RequestDelegate next, IConfiguration configuration, ILogger<GuestCartCookieMiddleware> logger)
        => (_next, _configuration, _logger) = (next, configuration, logger);

    public async Task Invoke(HttpContext context)
    {
        if (ShouldProvision(context))
        {
            var token = context.Request.Cookies[GuestCartIdentityProvider.CookieName];
            if (!GuestCartToken.IsWellFormed(token))
            {
                token = GuestCartToken.Create();
                var days = Math.Clamp(_configuration.GetValue<int?>("GuestCart:LifetimeDays") ?? 30, 1, 90);
                context.Response.Cookies.Append(GuestCartIdentityProvider.CookieName, token, GuestCartIdentityProvider.CookieOptions(context, days));
                _logger.LogInformation("GuestCartCreated EventType={EventType}", "GuestCartCreated");
            }
            context.Items[GuestCartIdentityProvider.RequestItemKey] = token;
        }
        await _next(context);
    }

    private static bool ShouldProvision(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true) return false;
        var path = context.Request.Path;
        return !path.StartsWithSegments("/api") && !path.StartsWithSegments("/admin") &&
               !path.StartsWithSegments("/_blazor") && !path.StartsWithSegments("/auth") &&
               !Path.HasExtension(path);
    }
}
