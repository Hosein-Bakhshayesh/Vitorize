using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Vitorize.Web.Services.Auth;

/// <summary>
/// On the shared-area paths (<c>/_blazor</c>, <c>/_framework</c>, <c>/media</c>) the resolver has to
/// pick ONE default scheme from cookies alone, and it prefers the admin session. That is right for
/// the admin shell, but a Blazor circuit serves both shells with one principal - so a storefront
/// header running on that circuit used to see the ADMIN identity: a browser holding an admin cookie
/// re-rendered the shop as signed-in the moment its circuit booted, even right after the customer
/// logged out, and the login button either vanished or died mid-click under the re-render.
///
/// This transformation makes the shared-path principal carry EVERY valid session identity instead of
/// one. Role and claim checks (<c>IsInRole</c>, <c>FindFirst</c>) search all identities, so the admin
/// shell keeps working exactly as before; storefront components select the customer identity
/// explicitly (<see cref="CustomerIdentityExtensions.CustomerIdentity"/>) and therefore render the
/// customer's true signed-in state regardless of which admin sessions coexist in the browser.
/// </summary>
public sealed class SharedCircuitIdentityEnricher(IHttpContextAccessor httpContextAccessor) : IClaimsTransformation
{
    private const string RecursionGuard = "vz-identity-enricher-active";

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var context = httpContextAccessor.HttpContext;
        if (context is null ||
            context.Items.ContainsKey(RecursionGuard) ||
            !SmartSchemeResolver.IsSharedAreaPath(context.Request.Path.Value))
        {
            return principal;
        }

        context.Items[RecursionGuard] = true;
        try
        {
            foreach (var scheme in new[] { VitorizeAuthSchemes.AdminScheme, VitorizeAuthSchemes.CustomerScheme })
            {
                if (principal.Identities.Any(i =>
                        i.IsAuthenticated && string.Equals(i.AuthenticationType, scheme, StringComparison.Ordinal)))
                {
                    continue;
                }

                var result = await context.AuthenticateAsync(scheme);
                if (result.Succeeded && result.Principal.Identity is ClaimsIdentity { IsAuthenticated: true } identity)
                    principal.AddIdentity(identity.Clone());
            }
        }
        finally
        {
            context.Items.Remove(RecursionGuard);
        }

        return principal;
    }
}

public static class CustomerIdentityExtensions
{
    /// <summary>
    /// The authenticated CUSTOMER identity, or null. Storefront chrome must key off this rather than
    /// <c>Identity.IsAuthenticated</c>: the circuit principal can be an administrator's while the
    /// customer session is signed out - and can carry both at once.
    /// </summary>
    public static ClaimsIdentity? CustomerIdentity(this ClaimsPrincipal user) =>
        user.Identities.FirstOrDefault(i =>
            i.IsAuthenticated &&
            string.Equals(i.AuthenticationType, VitorizeAuthSchemes.CustomerScheme, StringComparison.Ordinal));
}
