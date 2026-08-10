using Vitorize.Application.Cart;
using Vitorize.Application.Interfaces;

namespace Vitorize.Api.Services;

/// <summary>Central cart ownership resolver. A bearer customer identity always wins over a guest capability.</summary>
public sealed class CartIdentityResolver
{
    public const string GuestHeader = "X-Vitorize-Guest-Cart";
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<CartIdentityResolver> _logger;

    public CartIdentityResolver(ICurrentUserService currentUser, IHttpContextAccessor httpContextAccessor, ILogger<CartIdentityResolver> logger)
    {
        _currentUser = currentUser;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public CartIdentity Resolve()
    {
        if (_currentUser.UserId is { } userId)
        {
            _logger.LogDebug("GuestCartResolved Mode={Mode} EventType={EventType}", "Authenticated", "GuestCartResolved");
            return CartIdentity.ForUser(userId);
        }
        var token = _httpContextAccessor.HttpContext?.Request.Headers[GuestHeader].ToString();
        if (!GuestCartToken.IsWellFormed(token))
            throw new Vitorize.Shared.Exceptions.UnauthorizedException("شناسه سبد خرید مهمان معتبر نیست.");
        _logger.LogDebug("GuestCartResolved Mode={Mode} EventType={EventType}", "Guest", "GuestCartResolved");
        return CartIdentity.ForGuest(GuestCartToken.Hash(token!));
    }
}
