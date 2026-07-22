using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Regression coverage for the admin-panel authorization defect: when an admin session and a customer
/// session coexist in one browser, the Blazor interactive circuit (/_blazor, bare Origin, no Referer)
/// was downgraded to the customer scheme, so the admin dashboard returned "access denied". The admin
/// session must win for ambiguous framework/circuit requests, while real customer-page navigations
/// still resolve to the customer scheme.
/// </summary>
public sealed class SmartSchemeResolverTests
{
    private const string Bare = "https://vitorize.local";

    [Theory]
    [InlineData("/admin/dashboard")]
    [InlineData("/admin")]
    public void Admin_panel_paths_always_resolve_to_admin_scheme(string path)
    {
        // Even with a customer cookie also present, an /admin page is the admin scheme.
        var scheme = SmartSchemeResolver.Resolve(path, referer: null, origin: Bare, hasAdmin: true, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, scheme);
    }

    [Fact]
    public void Blazor_circuit_with_only_admin_cookie_resolves_to_admin()
    {
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: null, origin: Bare, hasAdmin: true, hasCustomer: false);
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, scheme);
    }

    [Fact]
    public void Blazor_circuit_with_both_cookies_and_bare_origin_resolves_to_admin()
    {
        // THE REGRESSION: a coexisting/stale customer cookie must NOT downgrade the admin circuit.
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: "", origin: Bare, hasAdmin: true, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, scheme);
    }

    [Fact]
    public void Blazor_circuit_with_only_customer_cookie_resolves_to_customer()
    {
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: null, origin: Bare, hasAdmin: false, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, scheme);
    }

    [Fact]
    public void Request_from_concrete_customer_page_keeps_customer_scheme_even_with_admin_cookie()
    {
        // Support staff validating a customer flow from an actual customer page (Referer has a real,
        // non-admin path) still gets the customer identity.
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: $"{Bare}/cart", origin: Bare, hasAdmin: true, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, scheme);
    }

    [Fact]
    public void Request_with_admin_referer_resolves_to_admin_even_with_customer_cookie()
    {
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: $"{Bare}/admin/products", origin: Bare, hasAdmin: true, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, scheme);
    }

    [Fact]
    public void Bare_root_referer_is_not_treated_as_a_concrete_customer_page()
    {
        // Origin/root "/" has no page context, so it must not downgrade an admin session.
        var scheme = SmartSchemeResolver.Resolve("/_blazor/negotiate", referer: $"{Bare}/", origin: Bare, hasAdmin: true, hasCustomer: true);
        Assert.Equal(VitorizeAuthSchemes.AdminScheme, scheme);
    }

    [Fact]
    public void No_session_cookies_default_to_customer_scheme()
    {
        // Anonymous access is denied by authorization, not by scheme selection; the default is customer.
        var scheme = SmartSchemeResolver.Resolve("/some/page", referer: null, origin: Bare, hasAdmin: false, hasCustomer: false);
        Assert.Equal(VitorizeAuthSchemes.CustomerScheme, scheme);
    }
}
