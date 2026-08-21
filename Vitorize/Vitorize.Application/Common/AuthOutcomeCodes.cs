namespace Vitorize.Application.Common;

/// <summary>
/// Machine-readable outcomes for the authentication endpoints.
///
/// These exist so the web layer never has to recognise a Persian sentence to decide what to do. The
/// user-facing wording lives in the web project and can change freely; these codes are the contract.
/// </summary>
public static class AuthOutcomeCodes
{
    /// <summary>A one-time code was issued and sent.</summary>
    public const string OtpSent = "OtpSent";

    /// <summary>
    /// No account exists for the supplied mobile. This is deliberately distinguishable: the product
    /// requires telling the visitor to register rather than leaving them at a code screen that will
    /// never receive a message. Nothing beyond "not registered" is disclosed — no identifier, no
    /// email, no status, and no hint about any other account.
    /// </summary>
    public const string RequiresRegistration = "RequiresRegistration";

    /// <summary>
    /// The account exists but cannot sign in (inactive, suspended or blocked). Kept separate from
    /// <see cref="RequiresRegistration"/> so a blocked customer is never told to register again,
    /// which would be both wrong and a way to create duplicate accounts.
    /// </summary>
    public const string AccountNotEligible = "AccountNotEligible";

    /// <summary>The account exists and the supplied password was wrong.</summary>
    public const string InvalidCredentials = "InvalidCredentials";
}
