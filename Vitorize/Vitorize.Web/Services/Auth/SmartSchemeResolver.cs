namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// Chooses the cookie authentication scheme when an admin session and a customer session can
    /// coexist in the same browser.
    ///
    /// The Blazor interactive circuit connects to <c>/_blazor</c>, a path that does not contain the
    /// <c>/admin</c> segment and whose WebSocket handshake carries only a bare <c>Origin</c> (scheme +
    /// host, no page path) and no <c>Referer</c>. The previous logic treated that bare origin as a
    /// "customer page" and downgraded the request to the customer scheme whenever a customer cookie
    /// also existed - so an admin's interactive circuit authenticated as the customer and the admin
    /// panel returned "access denied" whenever a stale customer cookie was present.
    ///
    /// This resolver only chooses the customer scheme for a request that clearly originates from a
    /// concrete, non-admin customer/public <b>page</b> (a Referer with a real path). Ambiguous
    /// framework/circuit requests fall through to the admin scheme when an admin cookie is present.
    /// An admin cookie is only issued after a validated admin sign-in, so preferring it can never
    /// escalate a non-admin, and role/permission authorization still runs on top.
    /// </summary>
    public static class SmartSchemeResolver
    {
        public static string Resolve(
            string requestPath,
            string? referer,
            string? origin,
            bool hasAdmin,
            bool hasCustomer)
        {
            // Admin panel pages resolve to the admin cookie by path.
            if (IsAdminPath(requestPath))
                return VitorizeAuthSchemes.AdminScheme;

            // Any request that clearly came from an admin page (Referer/Origin under /admin) is admin.
            if (ContainsAdminSegment(referer) || ContainsAdminSegment(origin))
                return VitorizeAuthSchemes.AdminScheme;

            // Downgrade to the customer identity only when the request clearly originates from a
            // concrete, non-admin customer/public page. A bare Origin with no path (the Blazor
            // /_blazor circuit handshake) is ambiguous and must never override an admin session.
            if (hasCustomer && IsConcreteNonAdminPage(referer))
                return VitorizeAuthSchemes.CustomerScheme;

            // An admin session takes precedence for ambiguous framework/circuit requests so the admin
            // panel's interactive circuit stays authenticated even when a customer cookie also exists.
            if (hasAdmin)
                return VitorizeAuthSchemes.AdminScheme;

            return VitorizeAuthSchemes.CustomerScheme;
        }

        private static bool IsAdminPath(string? path) =>
            !string.IsNullOrEmpty(path) &&
            path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);

        private static bool ContainsAdminSegment(string? value) =>
            !string.IsNullOrEmpty(value) &&
            value.Contains("/admin", StringComparison.OrdinalIgnoreCase);

        private static bool IsConcreteNonAdminPage(string? referer)
        {
            if (string.IsNullOrEmpty(referer) ||
                !Uri.TryCreate(referer, UriKind.Absolute, out var uri))
                return false;

            var path = uri.AbsolutePath;
            return path.Length > 1 &&
                   !path.StartsWith("/admin", StringComparison.OrdinalIgnoreCase);
        }
    }
}
