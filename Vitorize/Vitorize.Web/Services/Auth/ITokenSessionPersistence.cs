namespace Vitorize.Web.Services.Auth;

/// <summary>Persists a successful token rotation outside the rendered Blazor response.</summary>
public interface ITokenSessionPersistence
{
    Task<bool> PersistAsync(string scheme, string accessToken, string refreshToken, CancellationToken cancellationToken);
}
