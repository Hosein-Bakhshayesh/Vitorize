using Microsoft.Extensions.Hosting;

namespace Vitorize.Web.Services;

/// <summary>
/// Resolved CKEditor 5 licensing for the running environment.
///
/// Vitorize is a proprietary/commercial application, so the GPL key is only
/// permitted for non-Production environments and must be configured explicitly.
/// Production requires a real commercial license key supplied out-of-band
/// (environment variable <c>CkEditor__LicenseKey</c> or the secret manager) and
/// the host fails fast at startup if the key is missing or empty.
///
/// As a documented, temporary exception, Production may run under the GPL key
/// when — and only when — <c>CkEditor:AllowGplInProduction</c> is explicitly set
/// to <c>true</c> (supplied via <c>CkEditor__AllowGplInProduction</c>). In that
/// mode the host logs a warning and keeps the "Powered by CKEditor" badge; this
/// is a technical safeguard, not a statement of legal compliance.
///
/// The commercial key is a runtime value shipped to the browser by CKEditor's
/// own design; it is treated as a non-committed secret here so it never lands in
/// <c>appsettings.json</c>. See docs/ckeditor-license.md.
/// </summary>
public sealed class CkEditorOptions
{
    public const string GplLicenseKey = "GPL";
    private const string LicenseKeyConfig = "CkEditor:LicenseKey";
    private const string AllowGplInProductionConfig = "CkEditor:AllowGplInProduction";

    /// <summary>Exact warning emitted when Production boots under GPL mode.</summary>
    public const string GplInProductionWarning =
        "CKEditor 5 is running in GPL mode in Production. Ensure the application complies with the applicable GPL license obligations.";

    public required string LicenseKey { get; init; }

    public bool IsGpl => string.Equals(LicenseKey, GplLicenseKey, StringComparison.OrdinalIgnoreCase);

    /// <summary>True only when Production has been explicitly opted into GPL mode.</summary>
    public bool IsGplInProduction { get; init; }

    /// <summary>
    /// Reads and validates the configured license key for the given environment.
    /// Throws <see cref="InvalidOperationException"/> on Production when the key is
    /// absent/empty, or is GPL without <c>CkEditor:AllowGplInProduction=true</c> —
    /// the caller invokes this during startup so the host never boots a Production
    /// node with an unlicensed/unintended CKEditor configuration.
    /// </summary>
    public static CkEditorOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[LicenseKeyConfig]?.Trim();
        var isGpl = string.Equals(configured, GplLicenseKey, StringComparison.OrdinalIgnoreCase);
        // Defaults to false when missing, empty or non-boolean.
        var allowGplInProduction = bool.TryParse(configuration[AllowGplInProductionConfig], out var allow) && allow;

        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException(
                    "CKEditor 5 license is not configured for Production. Set a commercial license key via " +
                    "the 'CkEditor__LicenseKey' environment variable (or your secret manager). See docs/ckeditor-license.md.");

            if (isGpl && !allowGplInProduction)
                throw new InvalidOperationException(
                    "CKEditor 5 is set to the GPL key in Production, which is not permitted by default. Set a " +
                    "commercial license key via 'CkEditor__LicenseKey', or — to run temporarily under GPL — set " +
                    "'CkEditor__AllowGplInProduction=true' and ensure GPL obligations are met. See docs/ckeditor-license.md.");

            // Reaching here with isGpl implies allowGplInProduction == true.
            return new CkEditorOptions { LicenseKey = configured!, IsGplInProduction = isGpl };
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
