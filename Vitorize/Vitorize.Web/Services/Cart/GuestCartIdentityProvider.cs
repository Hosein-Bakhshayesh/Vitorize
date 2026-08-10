using Vitorize.Application.Cart;

namespace Vitorize.Web.Services.Cart;

/// <summary>Reads the HttpOnly guest capability from the current browser request without exposing it to UI code.</summary>
public sealed class GuestCartIdentityProvider
{
    public const string CookieName = "Vitorize.GuestCart";
    public const string RequestItemKey = "Vitorize.GuestCart.Token";
    private readonly IHttpContextAccessor _httpContextAccessor;
    private string? _token;

    public GuestCartIdentityProvider(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    public string? GetToken()
    {
        if (_token is not null) return _token;
        var context = _httpContextAccessor.HttpContext;
        var token = context?.Items.TryGetValue(RequestItemKey, out var provisioned) == true
            ? provisioned as string
            : context?.Request.Cookies[CookieName];
        _token = GuestCartToken.IsWellFormed(token) ? token : null;
        return _token;
    }

    public static CookieOptions CookieOptions(HttpContext context, int lifetimeDays) => new()
    {
        HttpOnly = true,
        Secure = Vitorize.Web.Services.Auth.AuthCookiePolicy.IsSecure(context),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = DateTimeOffset.UtcNow.AddDays(lifetimeDays)
    };
}
