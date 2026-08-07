using Microsoft.Extensions.Hosting;

namespace Vitorize.Web.Services;

/// <summary>
/// Resolves and validates CKEditor 5 licensing from ordinary application configuration.
/// Production requires a commercial key unless GPL has been deliberately enabled.
/// </summary>
public sealed class CkEditorOptions
{
    public const string GplLicenseKey = "GPL";
    private const string LicenseKeyConfig = "CkEditor:LicenseKey";
    private const string AllowGplInProductionConfig = "CkEditor:AllowGplInProduction";

    public const string GplInProductionWarning =
        "CKEditor 5 is running in GPL mode in Production. Ensure the application complies with the applicable GPL license obligations.";

    public required string LicenseKey { get; init; }

    public bool IsGpl => string.Equals(LicenseKey, GplLicenseKey, StringComparison.OrdinalIgnoreCase);

    public bool IsGplInProduction { get; init; }

    public static CkEditorOptions Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var configured = configuration[LicenseKeyConfig]?.Trim();
        var isGpl = string.Equals(configured, GplLicenseKey, StringComparison.OrdinalIgnoreCase);
        var allowGplInProduction = bool.TryParse(configuration[AllowGplInProductionConfig], out var allow) && allow;

        if (environment.IsProduction())
        {
            if (string.IsNullOrWhiteSpace(configured))
                throw new InvalidOperationException(
                    "CKEditor 5 license is not configured for Production. Set CkEditor:LicenseKey in appsettings.Production.json. See docs/ckeditor-license.md.");

            if (isGpl && !allowGplInProduction)
                throw new InvalidOperationException(
                    "CKEditor 5 is set to the GPL key in Production, which is not permitted by default. Set a commercial license key, or set CkEditor:AllowGplInProduction=true and ensure GPL obligations are met. See docs/ckeditor-license.md.");

            return new CkEditorOptions { LicenseKey = configured, IsGplInProduction = isGpl };
        }

        return new CkEditorOptions
        {
            LicenseKey = string.IsNullOrWhiteSpace(configured) ? GplLicenseKey : configured
        };
    }
}
