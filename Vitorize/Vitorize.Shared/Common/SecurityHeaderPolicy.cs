namespace Vitorize.Shared.Common;

public static class SecurityHeaderPolicy
{
    public const string ContentTypeOptions = "nosniff";
    public const string ReferrerPolicy = "strict-origin-when-cross-origin";
    public const string PermissionsPolicy = "geolocation=(), microphone=(), camera=(), payment=()";
    public const string ApiFrameOptions = "DENY";
    public const string WebFrameOptions = "SAMEORIGIN";

    public const string ApiContentSecurityPolicy =
        "default-src 'none'; frame-ancestors 'none'; base-uri 'none'; form-action 'none'";

    public const string WebContentSecurityPolicy =
        "default-src 'self'; base-uri 'self'; frame-ancestors 'self'; object-src 'none'; " +
        "img-src 'self' data: https:; font-src 'self' data: https:; " +
        "style-src 'self' 'unsafe-inline'; script-src 'self' 'unsafe-inline'; " +
        "connect-src 'self' https: wss:; form-action 'self'; upgrade-insecure-requests";
}
