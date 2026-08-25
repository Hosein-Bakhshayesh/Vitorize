using System.Security.Claims;

namespace Vitorize.Web.Services.UI;

/// <summary>
/// Resolves the single maintenance-mode business rule shared by the storefront
/// layout and the HTTP pipeline. The storefront blackout applies to everyone,
/// administrators included: operating the site during maintenance happens under
/// /admin (always reachable) and through the API's own admin bypass, never by
/// browsing the closed shop. A role exemption here used to let a browser holding
/// an admin cookie watch the maintenance page silently swap back to the live
/// shop once its circuit booted - the very leak it now prevents.
/// </summary>
public sealed class StorefrontMaintenanceService(StoreBrandingService branding)
{
    public async Task<StorefrontMaintenanceState?> GetStateAsync(ClaimsPrincipal? user)
    {
        var storeBranding = await branding.GetAsync();
        return storeBranding.MaintenanceMode
            ? new StorefrontMaintenanceState(storeBranding)
            : null;
    }
}

public sealed record StorefrontMaintenanceState(StoreBranding Branding);
