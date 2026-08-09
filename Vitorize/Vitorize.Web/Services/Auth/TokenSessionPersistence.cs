using Microsoft.JSInterop;

namespace Vitorize.Web.Services.Auth;

public sealed class TokenSessionPersistence : ITokenSessionPersistence
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IJSRuntime _js;
    private readonly ILogger<TokenSessionPersistence> _logger;

    public TokenSessionPersistence(IHttpContextAccessor httpContextAccessor, IJSRuntime js, ILogger<TokenSessionPersistence> logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _js = js;
        _logger = logger;
    }

    public async Task<bool> PersistAsync(string scheme, string accessToken, string refreshToken, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is not null && !context.Response.HasStarted)
            return await AuthSessionCookieWriter.PersistAsync(context, scheme, accessToken, refreshToken);

        try
        {
            // Interactive Blazor responses have already started. The browser makes this
            // same-origin call so Set-Cookie is applied to its real cookie jar.
            return await _js.InvokeAsync<bool>("vzAuthSession.persistTokens", cancellationToken, scheme, accessToken, refreshToken);
        }
        catch (JSDisconnectedException) { return false; }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to persist rotated tokens for {Scheme}", scheme);
            return false;
        }
    }
}
