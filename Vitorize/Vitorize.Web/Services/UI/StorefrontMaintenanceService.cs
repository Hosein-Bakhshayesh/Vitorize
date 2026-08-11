using System.Security.Claims;

namespace Vitorize.Web.Services.UI;

/// <summary>
/// Resolves the single maintenance-mode business rule shared by the storefront
/// layout and the HTTP pipeline. Administrators retain access while maintenance
/// is enabled so they can operate the site.
/// </summary>
public sealed class StorefrontMaintenanceService(StoreBrandingService branding)
{
    public async Task<StorefrontMaintenanceState?> GetStateAsync(ClaimsPrincipal? user)
    {
        var storeBranding = await branding.GetAsync();
        return storeBranding.MaintenanceMode && !IsAdministrator(user)
            ? new StorefrontMaintenanceState(storeBranding)
            : null;
    }

    private static bool IsAdministrator(ClaimsPrincipal? user) =>
        user?.IsInRole("Admin") == true || user?.IsInRole("SuperAdmin") == true;
}

public sealed record StorefrontMaintenanceState(StoreBranding Branding);
