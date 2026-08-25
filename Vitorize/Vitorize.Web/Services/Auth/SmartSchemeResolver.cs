namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// Chooses the cookie authentication scheme when an admin session and a customer session can
    /// coexist in the same browser.
    ///
    /// The decision is made from the request path and which session cookies exist - nothing else.
    /// An earlier version also consulted the <c>Referer</c> and <c>Origin</c> headers, which was
    /// wrong twice over. They are client-supplied, so a header could steer which identity the server
    /// adopted; and they are unreliable, because <c>Referrer-Policy: strict-origin-when-cross-origin</c>
    /// trims the path away and the WebSocket handshake sends no <c>Referer</c> at all. Worse, the
    /// header rule is what made storefront logout look broken: after the customer cookie was deleted,
    /// a plain <c>GET /</c> fell through to "an admin cookie exists, so use the admin scheme", and the
    /// storefront header re-rendered as a signed-in admin. Signing out had worked; the page disagreed.
    ///
    /// The rules, in order:
    ///
    ///   1. <c>/admin…</c> is the admin panel, so it is always the admin scheme.
    ///   2. <c>/_blazor</c> and <c>/_framework</c> are shared by both shells and carry no area in the
    ///      path. They are the one genuinely ambiguous case, and they are resolved by cookie presence
    ///      alone, preferring the admin session. That preference is deliberate: an admin cookie is
    ///      only issued after a validated admin sign-in, so it can never escalate a non-admin, and it
    ///      keeps an admin's interactive circuit authenticated when a stale customer cookie is also
    ///      present - the defect this resolver originally existed to fix. A circuit takes its rendered
    ///      authentication state from the page request that started it, which rule 1 or rule 3 has
    ///      already resolved correctly, and area-scoped policies (AdminOnly, CustomerOnly) name their
    ///      own scheme regardless of this default.
    ///   3. Every other path is customer-facing. With no customer cookie the request is simply
    ///      anonymous, even when an admin cookie exists.
    /// </summary>
    public static class SmartSchemeResolver
    {
        /// <summary>
        /// Paths both areas legitimately use, where the path alone cannot say which identity applies.
        ///
        /// <c>/_blazor</c> and <c>/_framework</c> are one transport shared by both shells.
        /// <c>/media</c> streams protected documents by forwarding the caller's own access token to the
        /// API, which decides what that caller may see - a customer their own documents, an
        /// administrator any of them. Treating it as customer-only signed administrators out of it.
        /// </summary>
        private static readonly string[] SharedAreaPaths = ["/_blazor", "/_framework", "/media"];

        public static string Resolve(string requestPath, bool hasAdmin, bool hasCustomer)
        {
            if (IsAdminPath(requestPath))
                return VitorizeAuthSchemes.AdminScheme;

            if (IsSharedAreaPath(requestPath))
                return hasAdmin ? VitorizeAuthSchemes.AdminScheme : VitorizeAuthSchemes.CustomerScheme;

            // A customer-facing path is never the admin identity. Without a customer cookie the
            // request is anonymous, which is exactly what a signed-out storefront should be.
            _ = hasCustomer;
            return VitorizeAuthSchemes.CustomerScheme;
        }

        private static bool IsAdminPath(string? path) =>
            !string.IsNullOrEmpty(path) &&
            path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        /// <summary>Public so the circuit identity enricher can scope itself to exactly these paths.</summary>
        public static bool IsSharedAreaPath(string? path) =>
            !string.IsNullOrEmpty(path) &&
            SharedAreaPaths.Any(prefix => path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
