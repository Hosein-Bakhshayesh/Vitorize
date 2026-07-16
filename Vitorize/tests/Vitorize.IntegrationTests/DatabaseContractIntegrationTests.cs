using System.Text.Json;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class DatabaseContractIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public DatabaseContractIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Canonical_deployment_records_every_required_script_with_manifest_hash()
    {
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            Path.Combine(_fixture.RepositoryRoot, "Database", "deployment-manifest.json")));
        var expected = document.RootElement.GetProperty("scripts").EnumerateArray()
            .Where(x => x.GetProperty("classification").GetString() == "required" &&
                        x.GetProperty("environments").EnumerateArray().Any(e => e.GetString() == "Development"))
            .ToDictionary(x => x.GetProperty("version").GetString()!, x => x.GetProperty("sha256").GetString()!);

        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT ScriptVersion, ScriptHash FROM dbo.DatabaseScriptHistory WHERE Success = 1";
        await using var reader = await command.ExecuteReaderAsync();
        var actual = new Dictionary<string, string>();
        while (await reader.ReadAsync())
            actual.Add(reader.GetString(0), reader.GetString(1));

        actual.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public async Task Phase2_and_phase3_SQL_verification_suites_pass()
    {
        await _fixture.RunSqlFileAsync(Path.Combine("Database", "Tests", "verify_phase2_financial_security.sql"));
        await _fixture.RunSqlFileAsync(Path.Combine("Database", "Tests", "verify_phase3_seo_content.sql"));
    }

    [Fact]
    public async Task Startup_seeds_reference_roles_but_no_privileged_or_demo_users()
    {
        await using var db = _fixture.CreateDbContext();
        (await db.Roles.OrderBy(x => x.Name).Select(x => x.Name).ToListAsync())
            .Should().BeEquivalentTo("Admin", "Customer", "SuperAdmin", "Support");
        _fixture.InitialPrivilegedUserCount.Should().Be(0);
    }

    [Fact]
    public async Task Seeder_is_idempotent_and_preserves_an_existing_setting_value()
    {
        const string key = "SiteName";
        await using (var db = _fixture.CreateDbContext())
        {
            var setting = await db.Settings.SingleAsync(x => x.Key == key);
            setting.Value = "Integration Preserved Value";
            await db.SaveChangesAsync();
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>().SeedAsync();

        await using var verify = _fixture.CreateDbContext();
        (await verify.Settings.SingleAsync(x => x.Key == key)).Value.Should().Be("Integration Preserved Value");
        (await verify.Settings.CountAsync(x => x.Key == key)).Should().Be(1);
    }

    [Theory]
    [InlineData("Users", "UX_Users_Mobile")]
    [InlineData("Orders", "UX_Orders_OrderNumber")]
    [InlineData("Payments", "UX_Payments_Gateway_Authority")]
    [InlineData("Payments", "UX_Payments_IdempotencyKey")]
    [InlineData("WalletTransactions", "UX_WalletTransactions_FinancialReference")]
    [InlineData("ProductInputFields", "UX_ProductInputFields_Product_Key")]
    [InlineData("OrderItemInputValues", "UX_OrderItemInputValues_Item_Key")]
    [InlineData("SmsMessages", "UX_SmsMessages_IdempotencyKey")]
    public async Task Critical_unique_indexes_exist_and_are_enabled(string table, string index)
    {
        await using var connection = new SqlConnection(_fixture.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(@table) AND name = @index AND is_unique = 1 AND is_disabled = 0;
            """;
        command.Parameters.AddWithValue("@table", $"dbo.{table}");
        command.Parameters.AddWithValue("@index", index);
        Convert.ToInt32(await command.ExecuteScalarAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Duplicate_mobile_is_rejected_by_SQL_Server_not_only_application_validation()
    {
        var mobile = $"09{Random.Shared.NextInt64(100000000, 999999999)}";
        await using var db = _fixture.CreateDbContext();
        db.Users.AddRange(NewUser(mobile), NewUser(mobile));

        var action = () => db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Foreign_keys_and_restrict_delete_behaviors_are_enforced_by_SQL_Server()
    {
        await using (var invalid = _fixture.CreateDbContext())
        {
            invalid.Products.Add(NewProduct(Guid.NewGuid(), $"missing-fk-{Guid.NewGuid():N}"));
            Func<Task> action = () => invalid.SaveChangesAsync();
            await action.Should().ThrowAsync<DbUpdateException>();
        }

        var category = NewCategory();
        var product = NewProduct(category.Id, $"restricted-delete-{Guid.NewGuid():N}");
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product); await seed.SaveChangesAsync();
        }
        await using (var delete = _fixture.CreateDbContext())
        {
            delete.Categories.Remove(await delete.Categories.SingleAsync(x => x.Id == category.Id));
            Func<Task> action = () => delete.SaveChangesAsync();
            await action.Should().ThrowAsync<DbUpdateException>();
        }
    }

    [Fact]
    public async Task Monetary_precision_is_applied_by_the_database_not_only_the_CLR_model()
    {
        var category = NewCategory();
        var product = NewProduct(category.Id, $"precision-{Guid.NewGuid():N}");
        product.BasePrice = 123.456m;
        await using (var db = _fixture.CreateDbContext())
        {
            db.Categories.Add(category); db.Products.Add(product); await db.SaveChangesAsync();
        }
        await using var verify = _fixture.CreateDbContext();
        (await verify.Products.Where(x => x.Id == product.Id).Select(x => x.BasePrice).SingleAsync())
            .Should().Be(123.46m);
    }

    [Fact]
    public async Task Enabled_check_constraints_and_foreign_keys_cover_financial_tables()
    {
        await using var db = _fixture.CreateDbContext();
        var checks = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value] FROM sys.check_constraints
            WHERE is_disabled = 0 AND OBJECT_NAME(parent_object_id) IN
              ('Payments','WalletTransactions','GiftCodeReservations','PaymentRefunds','OutboxMessages')
            """).SingleAsync();
        checks.Should().BeGreaterThan(0);

        var foreignKeys = await db.Database.SqlQueryRaw<int>("""
            SELECT COUNT(*) AS [Value] FROM sys.foreign_keys
            WHERE is_disabled = 0 AND is_not_trusted = 0 AND OBJECT_NAME(parent_object_id) IN
              ('Orders','OrderItems','Payments','WalletTransactions','GiftCodeReservations')
            """).SingleAsync();
        foreignKeys.Should().BeGreaterThanOrEqualTo(8);
    }

    [Fact]
    public async Task Filtered_unique_email_constraint_rejects_duplicate_active_accounts()
    {
        var email = $"integration-{Guid.NewGuid():N}@example.test";
        await using var db = _fixture.CreateDbContext();
        var first = NewUser($"091{Random.Shared.Next(10000000, 99999999)}"); first.Email = email;
        var second = NewUser($"092{Random.Shared.Next(10000000, 99999999)}"); second.Email = email;
        db.Users.AddRange(first, second);
        Func<Task> action = () => db.SaveChangesAsync();
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    private static Category NewCategory() => new()
    {
        Id = Guid.NewGuid(), Title = "DB Contract Category", Slug = $"db-contract-{Guid.NewGuid():N}",
        IsActive = true, CreatedAt = DateTime.UtcNow
    };

    private static Product NewProduct(Guid categoryId, string slug) => new()
    {
        Id = Guid.NewGuid(), CategoryId = categoryId, Title = "DB Contract Product", Slug = slug,
        ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual,
        BasePrice = 10, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
        IsActive = true, CreatedAt = DateTime.UtcNow
    };

    private static User NewUser(string mobile) => new()
    {
        Id = Guid.NewGuid(), FullName = "SQL constraint user", Mobile = mobile,
        PasswordHash = "not-a-real-password-hash", Status = 1, VerificationStatus = 0,
        IsMobileConfirmed = false, IsEmailConfirmed = false, CreatedAt = DateTime.UtcNow
    };
}
