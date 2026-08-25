using System.Net;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Web.Services;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Exercises the actual Web middleware/Razor pipeline with a deterministic
/// settings response. The API is intentionally not substituted by a helper
/// method: requests traverse the Web host exactly as a document request does.
/// </summary>
public sealed class StorefrontMaintenanceWebIntegrationTests : IAsyncLifetime
{
    private readonly StorefrontMaintenanceWebFactory _factory = new();

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await _factory.DisposeAsync();
        StoreBrandingService.Invalidate();
    }

    [Fact]
    public async Task Storefront_document_requests_preserve_maintenance_http_semantics_and_scope()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        _factory.SetMaintenance(false);
        (await client.GetAsync("/shop")).StatusCode.Should().Be(HttpStatusCode.OK);

        foreach (var status in new[] { 400, 401, 403, 404, 500, 503 })
        {
            var errorPage = await client.GetAsync($"/error/{status}");
            ((int)errorPage.StatusCode).Should().Be(status);
            (await errorPage.Content.ReadAsStringAsync()).Should().Contain("class=\"st-errpage");
        }

        _factory.SetMaintenance(true, "پیام تعمیرات آزمایشی");
        var maintenanceShop = await client.GetAsync("/shop");
        maintenanceShop.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await maintenanceShop.Content.ReadAsStringAsync()).Should().Contain("class=\"st-errpage");

        var maintenance503 = await client.GetAsync("/error/503");
        maintenance503.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await maintenance503.Content.ReadAsStringAsync()).Should().Contain("class=\"st-errpage");

        (await client.GetAsync("/css/storefront.css")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/admin/login")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Enhanced navigation is no longer exempt. Letting it through was how a customer who already
        // had the site open kept browsing after maintenance was switched on - the flag only ever
        // stopped fresh page loads. It is the same navigation to the same page, so it gets the same
        // answer.
        using var enhancedNavigation = new HttpRequestMessage(HttpMethod.Get, "/shop");
        enhancedNavigation.Headers.Add("blazor-enhanced-nav", "on");
        (await client.SendAsync(enhancedNavigation)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        // The customer panel is part of the shop, so it closes with it. Sign-in stays open because an
        // administrator has to be able to get in and switch maintenance back off.
        (await client.GetAsync("/customer/dashboard")).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        (await client.GetAsync("/auth/session-expired?area=customer")).StatusCode
            .Should().NotBe(HttpStatusCode.ServiceUnavailable);
    }
}

internal sealed class StorefrontMaintenanceWebFactory : WebApplicationFactory<global::Program>
{
    private bool _maintenanceEnabled;
    private string _maintenanceMessage = "پیام تعمیرات آزمایشی";

    public void SetMaintenance(bool enabled, string? message = null)
    {
        _maintenanceEnabled = enabled;
        if (!string.IsNullOrWhiteSpace(message)) _maintenanceMessage = message;
        StoreBrandingService.Invalidate();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApiSettings:BaseUrl"] = "https://settings.test/api/",
            ["ApiSettings:MediaBaseUrl"] = "https://settings.test",
            ["Seq:Enabled"] = "false"
        }));
        builder.ConfigureServices(services =>
        {
            services.AddHttpClient<ApiClient>()
                .ConfigurePrimaryHttpMessageHandler(() => new PublicSettingsHandler(this));
        });
    }

    private sealed class PublicSettingsHandler(StorefrontMaintenanceWebFactory factory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Method == HttpMethod.Get && request.RequestUri?.AbsolutePath.EndsWith("/api/settings/public", StringComparison.OrdinalIgnoreCase) == true)
            {
                var data = $$"""{"isSuccess":true,"message":"ok","data":[{"key":"MaintenanceMode","value":"{{factory._maintenanceEnabled.ToString().ToLowerInvariant()}}"},{"key":"MaintenanceMessage","value":"{{factory._maintenanceMessage}}"}]}""";
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(data, Encoding.UTF8, "application/json")
                });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }
}
