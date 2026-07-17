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

    public static string BuildWebContentSecurityPolicy(string? mediaBaseUrl)
    {
        if (!Uri.TryCreate(mediaBaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return WebContentSecurityPolicy;

        // HTTPS media is already covered by https:. An explicit HTTP source is only
        // admitted for an isolated loopback test host, never for a public deployment.
        if (uri.Scheme == Uri.UriSchemeHttps || !uri.IsLoopback)
            return WebContentSecurityPolicy;

        var origin = uri.GetLeftPart(UriPartial.Authority);
        return WebContentSecurityPolicy.Replace(
            "img-src 'self' data: https:;",
            $"img-src 'self' data: https: {origin};",
            StringComparison.Ordinal);
    }
}
