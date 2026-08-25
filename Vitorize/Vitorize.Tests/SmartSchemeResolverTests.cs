using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// The scheme decision is path-based and deterministic. Two defects meet here, so both directions
/// matter:
///
///   * the admin circuit must not be downgraded to the customer identity by a coexisting or stale
///     customer cookie (the original reason this resolver exists);
///   * a storefront page must not be upgraded to the admin identity just because an admin cookie
///     exists — that is what made customer logout appear to do nothing.
///
/// Request headers are no longer part of the decision at all. They are client-supplied, and
/// Referrer-Policy strips the path from them anyway, so they were both unsafe and unreliable.
/// </summary>
public sealed class SmartSchemeResolverTests
{
    // ---------------------------------------------------------------- admin paths

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/dashboard")]
    [InlineData("/admin/users")]
    [InlineData("/ADMIN/Settings")]
    public void Admin_panel_paths_always_resolve_to_admin(string path)
    {
        // True whatever the cookie jar holds: the path alone settles it.
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: true));
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: false));
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: false, hasCustomer: true));
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: false, hasCustomer: false));
    }

    // ---------------------------------------------------------------- customer-facing paths

    [Theory]
    [InlineData("/")]
    [InlineData("/shop")]
    [InlineData("/product/telegram-gifts")]
    [InlineData("/cart")]
    [InlineData("/checkout")]
    [InlineData("/customer/dashboard")]
    [InlineData("/login")]
    public void Customer_facing_paths_never_resolve_to_admin(string path)
    {
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, SmartSchemeResolver.Resolve(path, hasAdmin: false, hasCustomer: true));
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: true));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/shop")]
    [InlineData("/customer/profile")]
    public void A_storefront_page_with_only_an_admin_cookie_is_anonymous_not_admin(string path)
    {
        // THE LOGOUT DEFECT: the customer cookie has just been deleted and an admin cookie remains.
        // Resolving to the admin scheme here re-rendered the storefront header as signed in, so the
        // logout looked like it had failed. The customer scheme with no customer cookie is anonymous,
        // which is what a signed-out storefront must be.
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: false));
    }

    // ---------------------------------------------------------------- shared framework transport

    [Theory]
    [InlineData("/_blazor/negotiate")]
    [InlineData("/_blazor")]
    [InlineData("/_framework/blazor.web.js")]
    // Protected media forwards the caller's own token to the API, which decides what they may see, so
    // an administrator reviewing an identity document must not be treated as an anonymous customer.
    [InlineData("/media/verification-documents/2db2279f-3436-4662-a904-f70319024390")]
    public void Shared_area_paths_prefer_the_admin_session_when_one_exists(string path)
    {
        // /_blazor is one endpoint serving both shells, so the path carries no area. An admin cookie
        // only exists after a validated admin sign-in, so preferring it cannot escalate anyone, and it
        // keeps the admin circuit working when a stale customer cookie is also present.
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: true));
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, SmartSchemeResolver.Resolve(path, hasAdmin: true, hasCustomer: false));
    }

    [Theory]
    [InlineData("/_blazor/negotiate")]
    [InlineData("/_framework/blazor.web.js")]
    [InlineData("/media/verification-documents/2db2279f-3436-4662-a904-f70319024390")]
    public void Shared_area_paths_fall_back_to_customer_without_an_admin_cookie(string path)
    {
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, SmartSchemeResolver.Resolve(path, hasAdmin: false, hasCustomer: true));
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, SmartSchemeResolver.Resolve(path, hasAdmin: false, hasCustomer: false));
    }

    // ---------------------------------------------------------------- the whole matrix is total

    [Fact]
    public void Every_path_and_cookie_combination_resolves_to_a_real_scheme()
    {
        string[] paths = ["", "/", "/admin", "/admin/users", "/shop", "/customer/orders", "/_blazor/negotiate", "/_framework/x.js", "/media/verification-documents/x", "/auth/customer/logout"];
        foreach (var path in paths)
        {
            foreach (var admin in new[] { true, false })
            {
                foreach (var customer in new[] { true, false })
                {
                    var scheme = SmartSchemeResolver.Resolve(path, admin, customer);
                    Assert.True(
                        scheme == VitorizeAuthSchemes.AdminScheme || scheme == VitorizeAuthSchemes.CustomerScheme,
                        $"'{path}' admin={admin} customer={customer} produced '{scheme}'");
                }
            }
        }
    }

    [Fact]
    public void The_decision_takes_no_header_input()
    {
        // Guards the security property rather than a behaviour: if someone reintroduces a Referer or
        // Origin parameter, this fails and forces the conversation again.
        var parameters = typeof(SmartSchemeResolver)
            .GetMethod(nameof(SmartSchemeResolver.Resolve))!
            .GetParameters()
            .Select(p => p.Name)
            .ToArray();

        Assert.Equal(new[] { "requestPath", "hasAdmin", "hasCustomer" }, parameters);
    }
}
