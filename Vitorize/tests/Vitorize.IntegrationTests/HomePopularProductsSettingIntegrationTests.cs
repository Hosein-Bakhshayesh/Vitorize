using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Settings;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

/// <summary>
/// The popular-products toggle as a real settings row.
///
/// No schema change was needed for it: the seeder inserts keys that are missing and never overwrites
/// an existing value, so a deployment creates the row switched off and a later administrative choice
/// survives every redeploy. These tests hold that behaviour, and that the key reaches the storefront
/// through the public settings endpoint so the home page can read it.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class HomePopularProductsSettingIntegrationTests
{
    private const string Key = "HomePopularProductsEnabled";
    private readonly IntegrationTestFixture _fixture;
    public HomePopularProductsSettingIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task The_setting_exists_switched_off_with_an_admin_editable_shape()
    {
        await using var db = _fixture.CreateDbContext();
        var setting = await db.Settings.AsNoTracking().SingleOrDefaultAsync(x => x.Key == Key);

        setting.Should().NotBeNull("the seeder must create the row so Admin can toggle it");
        setting!.Value.Should().Be("false", "the section is hidden until an administrator enables it");
        // The admin settings page renders a switch for this ValueType and uses Description as label.
        setting.ValueType.Should().Be("bool");
        setting.GroupName.Should().Be("Homepage");
        setting.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task The_storefront_can_read_the_setting_from_the_public_endpoint()
    {
        using var client = _fixture.CreateClient();

        var response = await client.GetFromJsonAsync<ApiResult<List<SettingDto>>>("/api/settings/public");

        response!.Data.Should().Contain(x => x.Key == Key,
            "the home page resolves the toggle through the public settings payload");
    }

    [Fact]
    public async Task Re_running_the_seeder_never_overwrites_an_administrative_choice()
    {
        // The scenario that matters on an existing environment: an administrator turns the section on,
        // and the next deployment must leave it on. The row is shared with the rest of the suite, so
        // it is put back afterwards whatever happens.
        var original = await ReadAsync();
        try
        {
            await WriteAsync("true");

            using var scope = _fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>().SeedAsync();

            (await ReadAsync()).Should().Be("true", "a redeploy must not reset a deliberate choice");
        }
        finally
        {
            await WriteAsync(original ?? "false");
        }
    }

    [Fact]
    public async Task A_deployment_recreates_the_row_switched_off_when_it_is_missing()
    {
        // Equivalent of the existing production database, which has no such row yet.
        var original = await ReadAsync();
        try
        {
            await using (var db = _fixture.CreateDbContext())
            {
                db.Settings.RemoveRange(await db.Settings.Where(x => x.Key == Key).ToListAsync());
                await db.SaveChangesAsync();
            }

            using var scope = _fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>().SeedAsync();

            (await ReadAsync()).Should().Be("false");
        }
        finally
        {
            await WriteAsync(original ?? "false");
        }
    }

    // ---------------------------------------------------------------- helpers

    private async Task<string?> ReadAsync()
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Settings.AsNoTracking().Where(x => x.Key == Key)
            .Select(x => x.Value).SingleOrDefaultAsync();
    }

    private async Task WriteAsync(string value)
    {
        await using var db = _fixture.CreateDbContext();
        var setting = await db.Settings.SingleOrDefaultAsync(x => x.Key == Key);
        if (setting is null)
        {
            db.Settings.Add(new Setting
            {
                Id = Guid.NewGuid(), Key = Key, Value = value, GroupName = "Homepage",
                ValueType = "bool", Description = "نمایش محبوب‌ترین کالاها در صفحه اصلی",
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = value;
        }
        await db.SaveChangesAsync();
    }
}
