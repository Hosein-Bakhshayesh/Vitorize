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
        "img-src 'self' data: blob: https:; font-src 'self' data: https:; " +
        // Trusted site markup is deliberately restricted to administrator-managed settings. These
        // two official provider origins are needed for the Zarinpal trust badge and Gapify chat
        // widget; keeping the list explicit preserves the rest of the storefront CSP boundary.
        "style-src 'self' 'unsafe-inline' https://app.gapify.ai; " +
        "script-src 'self' 'unsafe-inline' https://www.zarinpal.com https://app.gapify.ai; " +
        "frame-src 'self' https://app.gapify.ai; " +
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
            "img-src 'self' data: blob: https:;",
            $"img-src 'self' data: blob: https: {origin};",
            StringComparison.Ordinal);
    }
}
