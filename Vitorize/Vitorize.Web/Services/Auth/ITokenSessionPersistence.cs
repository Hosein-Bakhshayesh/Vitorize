namespace Vitorize.Web.Services.Auth;

/// <summary>Persists a successful token rotation outside the rendered Blazor response.</summary>
public interface ITokenSessionPersistence
{
    Task<bool> PersistAsync(string scheme, string accessToken, string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Clears the browser's authentication and token cookies for one scheme.
    ///
    /// Needed because a rendered circuit has no HTTP response of its own: without this, ending a
    /// session mid-circuit cleared only the in-memory tokens and left the browser holding a cookie
    /// whose refresh token had already been revoked. Every later request then repopulated the dead
    /// token from that cookie, and the only way out was for the customer to clear their browser data.
    /// </summary>
    Task<bool> EndSessionAsync(string scheme, CancellationToken cancellationToken);
}
