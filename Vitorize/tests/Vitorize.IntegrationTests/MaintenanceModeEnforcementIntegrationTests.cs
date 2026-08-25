using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Maintenance mode has to actually stop people buying.
///
/// The previous implementation stamped a 503 on storefront pages and then served them anyway, so the
/// API stayed wide open: a customer with a page already loaded, or anything calling the API directly,
/// could still fill a cart, place an order and start a payment. The only test that existed checked
/// the layout of the /error/503 page and never switched the setting on, which is why a green suite
/// hid it.
///
/// The exception that matters most is the payment callback. Money may already have left a customer's
/// account by the time maintenance is switched on, and refusing the verification would strand it.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class MaintenanceModeEnforcementIntegrationTests : IAsyncLifetime
{
    private readonly IntegrationTestFixture _fixture;
    public MaintenanceModeEnforcementIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    public Task InitializeAsync() => SetMaintenanceAsync(false);

    // Never leave the shop closed for the rest of the suite.
    public Task DisposeAsync() => SetMaintenanceAsync(false);

    private async Task SetMaintenanceAsync(bool enabled)
    {
        await using (var db = _fixture.CreateDbContext())
        {
            var setting = await db.Settings.FirstOrDefaultAsync(x => x.Key == "MaintenanceMode");
            if (setting is null)
            {
                db.Settings.Add(new Setting
                {
                    Id = Guid.NewGuid(), Key = "MaintenanceMode", Value = enabled ? "true" : "false",
                    GroupName = "General", ValueType = "bool", UpdatedAt = DateTime.UtcNow
                });
            }
            else
            {
                setting.Value = enabled ? "true" : "false";
                setting.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();
        }

        // The provider caches; writing straight to the database bypasses the service that would have
        // invalidated it, so do that explicitly here.
        _fixture.Factory.Services.GetRequiredService<IMaintenanceStateProvider>().Invalidate();
    }

    // ---------------------------------------------------------------- blocked

    [Theory]
    [InlineData("POST", "/api/cart/items")]
    [InlineData("DELETE", "/api/cart/clear")]
    [InlineData("POST", "/api/checkout")]
    public async Task Purchase_endpoints_are_refused_while_maintenance_is_on(string method, string path)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync();
        using var client = _fixture.CreateClient(token);

        await SetMaintenanceAsync(true);

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        };
        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable,
            $"{method} {path} moves a purchase forward and must be refused");
        (await response.Content.ReadAsStringAsync()).Should().Contain("MaintenanceMode");
    }

    [Fact]
    public async Task A_blocked_checkout_creates_no_order()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync();
        using var client = _fixture.CreateClient(token);

        await SetMaintenanceAsync(true);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/checkout")
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        (await client.SendAsync(request)).StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0,
            "a refused request must not have reached the service");
    }

    // ---------------------------------------------------------------- allowed

    [Fact]
    public async Task The_zarinpal_callback_stays_open_so_paid_money_is_never_stranded()
    {
        using var client = _fixture.CreateClient();
        await SetMaintenanceAsync(true);

        // An unknown authority is still processed and answered on its own terms - what matters is that
        // maintenance did not refuse it outright.
        var response = await client.GetAsync("/api/payments/zarinpal/callback?Authority=A00000000000000000000000000000000000&Status=NOK");

        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable,
            "blocking payment verification would take money without confirming the order");
    }

    [Theory]
    [InlineData("/api/health/live")]
    [InlineData("/api/settings/public")]
    public async Task Operational_and_public_reads_stay_available(string path)
    {
        using var client = _fixture.CreateClient();
        await SetMaintenanceAsync(true);

        (await client.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Reads_of_an_existing_order_stay_available()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync();
        using var client = _fixture.CreateClient(token);
        await SetMaintenanceAsync(true);

        // Someone who already paid must still be able to look at what they bought.
        (await client.GetAsync("/api/orders")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task An_administrator_is_not_blocked_and_can_switch_it_back_off()
    {
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(adminToken);

        await SetMaintenanceAsync(true);

        // Reaching settings while maintenance is on is what makes the flag reversible.
        (await admin.GetAsync("/api/admin/settings")).StatusCode.Should().Be(HttpStatusCode.OK);

        var save = await admin.PostAsJsonAsync("/api/admin/settings", new
        {
            Key = "MaintenanceMode", Value = "false",
            GroupName = "General", ValueType = "bool", Description = "maintenance"
        });
        save.StatusCode.Should().Be(HttpStatusCode.OK);

        // Saving through the service invalidates the cache, so the next call is already unblocked.
        var (_, token) = await _fixture.CreateUserAndTokenAsync();
        using var customer = _fixture.CreateClient(token);
        (await customer.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ---------------------------------------------------------------- off

    [Theory]
    [InlineData("POST", "/api/cart/items")]
    [InlineData("POST", "/api/checkout")]
    public async Task Nothing_is_blocked_while_maintenance_is_off(string method, string path)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync();
        using var client = _fixture.CreateClient(token);

        await SetMaintenanceAsync(false);

        using var request = new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = JsonContent.Create(new { })
        };
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
        var response = await client.SendAsync(request);

        // The request may still fail on its own merits - an empty body is not a valid checkout - but it
        // must not be refused by maintenance.
        response.StatusCode.Should().NotBe(HttpStatusCode.ServiceUnavailable);
    }
}
