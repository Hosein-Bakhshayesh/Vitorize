namespace Vitorize.Web.Services.Auth
{
    /// <summary>
    /// Guards login/logout <c>returnUrl</c> handling against open-redirect attacks: only a local path
    /// rooted at a single '/' is accepted. Protocol-relative (<c>//host</c>), backslash tricks
    /// (<c>/\host</c>) and absolute URLs (<c>http://host</c>) fall back to a safe default.
    /// </summary>
    public static class SafeRedirect
    {
        public static string LocalOrDefault(string? returnUrl, string fallback)
        {
            if (string.IsNullOrWhiteSpace(returnUrl))
                return fallback;

            if (!returnUrl.StartsWith('/'))
                return fallback;

            // Reject protocol-relative and backslash-based bypasses (//evil.com, /\evil.com).
            if (returnUrl.StartsWith("//", StringComparison.Ordinal) ||
                returnUrl.StartsWith("/\\", StringComparison.Ordinal))
                return fallback;

            return returnUrl;
        }
    }
}
