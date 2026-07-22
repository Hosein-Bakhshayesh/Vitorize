using Microsoft.Extensions.Hosting;
using NSubstitute;
using Vitorize.Web.Services.Auth;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Cookie/redirect/scheme regressions for Admin + Customer authentication. Development must work over
/// the HTTP launch profile (Secure cookies are dropped by browsers over plain HTTP on non-localhost
/// hosts, breaking the post-login redirect), while Production always requires Secure. Also pins the
/// open-redirect guard and the admin/customer cookie-name separation.
/// </summary>
public sealed class AuthCookiePolicyTests
{
    private static IHostEnvironment Env(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = name;
        return environment;
    }

    [Fact]
    public void Production_always_requires_secure_cookies()
    {
        Assert.True(AuthCookiePolicy.IsSecure(Env("Production"), requestIsHttps: false));
        Assert.True(AuthCookiePolicy.IsSecure(Env("Production"), requestIsHttps: true));
    }

    [Fact]
    public void Development_mirrors_the_request_scheme()
    {
        // HTTPS dev -> secure; HTTP dev -> not secure so the cookie actually persists.
        Assert.True(AuthCookiePolicy.IsSecure(Env("Development"), requestIsHttps: true));
        Assert.False(AuthCookiePolicy.IsSecure(Env("Development"), requestIsHttps: false));
    }

    [Fact]
    public void Staging_or_other_non_development_environments_require_secure()
    {
        Assert.True(AuthCookiePolicy.IsSecure(Env("Staging"), requestIsHttps: false));
    }

    [Theory]
    [InlineData("/admin/products", "/admin/products")]
    [InlineData("/customer/orders", "/customer/orders")]
    [InlineData("//evil.com", null)]              // protocol-relative
    [InlineData("/\\evil.com", null)]             // backslash bypass
    [InlineData("https://evil.com", null)]        // absolute URL
    [InlineData("javascript:alert(1)", null)]     // scheme, not local
    [InlineData("", null)]                          // empty
    [InlineData(null, null)]                         // missing
    public void Return_url_only_allows_local_paths(string? returnUrl, string? expectedOrFallback)
    {
        const string fallback = "/admin/dashboard";
        var result = SafeRedirect.LocalOrDefault(returnUrl, fallback);
        Assert.Equal(expectedOrFallback ?? fallback, result);
    }

    [Fact]
    public void Admin_and_customer_cookie_names_are_distinct()
    {
        Assert.NotEqual(VitorizeAuthSchemes.AdminAuthCookie, VitorizeAuthSchemes.CustomerAuthCookie);
        Assert.NotEqual(VitorizeAuthSchemes.AdminScheme, VitorizeAuthSchemes.CustomerScheme);
        Assert.NotEqual(VitorizeAuthSchemes.AdminAccessTokenCookie, VitorizeAuthSchemes.CustomerAccessTokenCookie);
    }
}
