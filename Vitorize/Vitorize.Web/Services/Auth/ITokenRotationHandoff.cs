namespace Vitorize.Web.Services.Auth;

/// <summary>
/// Holds a rotated token pair that could not be written to the browser, so the next request that
/// <i>can</i> write cookies finishes the job.
///
/// Rotation happens wherever the session needs it, including inside an interactive circuit whose
/// response has already started and where JavaScript interop may be unavailable. When the write
/// fails the API has already spent the old refresh token, so the browser is left holding a revoked
/// one: the circuit works until it ends, and the next page load presents the dead token. That is the
/// divergence behind "I had to clear my cookies".
///
/// The pair is therefore parked here, keyed by the <b>old</b> refresh token — the one value the
/// browser still presents — and claimed on the next request that has a real HTTP response to attach
/// Set-Cookie to. Entries are one-shot and expire quickly; the API's own rotation grace window covers
/// the same interval, so the two agree on what the session is during the handover.
/// </summary>
public interface ITokenRotationHandoff
{
    /// <summary>Parks the new pair against the old refresh token the browser still holds.</summary>
    void Remember(string oldRefreshToken, string scheme, string accessToken, string refreshToken);

    /// <summary>Claims and removes a parked pair. One-shot: a second caller gets nothing.</summary>
    bool TryTake(string oldRefreshToken, out RotatedTokens? tokens);
}

/// <summary>A rotated pair awaiting durable storage in the browser.</summary>
public sealed record RotatedTokens(string Scheme, string AccessToken, string RefreshToken);
