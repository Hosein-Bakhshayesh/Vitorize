using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Cart;
using Vitorize.Web.Middleware;
using Vitorize.Web.Services.Cart;
using Xunit;

namespace Vitorize.Tests;

public sealed class GuestCartCookieMiddlewareTests
{
    [Fact]
    public async Task Storefront_request_gets_an_httponly_secure_guest_capability_without_exposing_it_in_the_url()
    {
        var environment = new TestHostEnvironment { EnvironmentName = Environments.Production };
        var services = new ServiceCollection().AddSingleton<IHostEnvironment>(environment).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = "/shop";
        context.Request.IsHttps = true;
        var middleware = new GuestCartCookieMiddleware(_ => Task.CompletedTask,
            new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["GuestCart:LifetimeDays"] = "30" }).Build(),
            NullLogger<GuestCartCookieMiddleware>.Instance);

        await middleware.Invoke(context);

        var header = context.Response.Headers.SetCookie.Single();
        Assert.Contains($"{GuestCartIdentityProvider.CookieName}=", header, StringComparison.Ordinal);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        var token = Assert.IsType<string>(context.Items[GuestCartIdentityProvider.RequestItemKey]);
        Assert.True(GuestCartToken.IsWellFormed(token));
        Assert.DoesNotContain(token, context.Request.QueryString.Value ?? string.Empty, StringComparison.Ordinal);
    }

    private sealed class TestHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Vitorize.Tests";
        public string WebRootPath { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
