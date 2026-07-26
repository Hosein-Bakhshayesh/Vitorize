using Microsoft.Extensions.Hosting;

namespace Vitorize.Web.Services;

/// <summary>
/// Resolved CKEditor 5 licensing for the running environment.
///
/// Vitorize is a proprietary/commercial application, so the GPL key is only
/// permitted for non-Production environments and must be configured explicitly.
/// Production requires a real commercial license key supplied out-of-band
/// (environment variable <c>CkEditor__LicenseKey</c> or the secret manager) and
/// the host fails fast at startup if it is missing, empty or set to "GPL".
///
/// The commercial key is a runtime value shipped to the browser by CKEditor's
/// own design; it is treated as a non-committed secret here so it never lands in
/// <c>appsettings.json</c>. See docs/ckeditor-license.md.
/// </summary>
public sealed class CkEditorOptions
{
    public const string GplLicenseKey = "GPL";
    private const string ConfigKey = "CkEditor:LicenseKey";

    public required string LicenseKey { get; init; }

    public bool IsGpl => string.Equals(LicenseKey, GplLicenseKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Reads and validates the configured license key for the given environment.
    /// Throws <see cref="InvalidOperationException"/> on Production when the key is
    /// absent, empty or GPL — the caller invokes this during startup so the host
    /// never boots a Production node with GPL branding/licensing.
    /// </summary>
    public static CkEditorOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[ConfigKey]?.Trim();
        var isGpl = string.Equals(configured, GplLicenseKey, StringComparison.OrdinalIgnoreCase);

        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(configured) || isGpl)
                throw new InvalidOperationException(
                    "CKEditor 5 license is not configured for Production. Set a commercial license key via " +
                    "the 'CkEditor__LicenseKey' environment variable (or your secret manager). Empty values " +
                    "and the 'GPL' key are not permitted in Production. See docs/ckeditor-license.md.");

            return new CkEditorOptions { LicenseKey = configured! };
        }

        // Non-Production (Development, Testing, …). GPL is allowed but must be an
        // explicit choice — an unconfigured key falls back to GPL for local
        // convenience only and never leaks into Production (guarded above).
        return new CkEditorOptions
        {
            LicenseKey = string.IsNullOrWhiteSpace(configured) ? GplLicenseKey : configured!
        };
    }
}
