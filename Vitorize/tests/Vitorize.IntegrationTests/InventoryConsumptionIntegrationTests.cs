using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Managed-inventory consumption against real SQL Server.
///
/// The production decrement in PaymentService.ConsumeManagedStockAsync is a single conditional
/// UPDATE guarded by a CHECK constraint. Those semantics — never negative, exactly one winner under
/// concurrency — depend on SQL Server behaviour and cannot be certified on EF InMemory, so these
/// tests issue the identical statement through the real provider.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class InventoryConsumptionIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public InventoryConsumptionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    /// <summary>The exact statement ConsumeManagedStockAsync issues.</summary>
    private static Task<int> ConsumeAsync(Vitorize.Infrastructure.Persistence.VitorizeDbContext db, Guid variantId, int quantity) =>
        db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariants
SET    StockQuantity = StockQuantity - {quantity}
WHERE  Id = {variantId}
  AND  StockQuantity >= {quantity}");

    private async Task<Guid> CreateManagedVariantAsync(int stock, byte deliveryType = (byte)DeliveryType.Manual)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "inv-cat", Slug = $"inv-cat-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "inv-product",
            Slug = $"inv-product-{Guid.NewGuid():N}", DeliveryType = deliveryType,
            BasePrice = 1000m, CurrencyType = (byte)CurrencyType.Toman, IsActive = true,
            MinOrderQuantity = 1, CreatedAt = DateTime.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "inv-variant", Price = 1000m,
            StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = stock,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category);
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return variant.Id;
    }

    private async Task<int> StockOfAsync(Guid variantId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ProductVariants.Where(x => x.Id == variantId).Select(x => x.StockQuantity).SingleAsync();
    }

    [Fact]
    public async Task Successful_consumption_decrements_by_exactly_the_ordered_quantity()
    {
        var variantId = await CreateManagedVariantAsync(5);

        await using (var db = _fixture.CreateDbContext())
            (await ConsumeAsync(db, variantId, 2)).Should().Be(1, "one row must be updated");

        (await StockOfAsync(variantId)).Should().Be(3);
    }

    [Fact]
    public async Task Consumption_beyond_available_stock_changes_nothing_and_reports_the_shortfall()
    {
        var variantId = await CreateManagedVariantAsync(3);

        int affected;
        await using (var db = _fixture.CreateDbContext())
            affected = await ConsumeAsync(db, variantId, 10);

        // Zero affected rows is the signal ConsumeManagedStockAsync turns into a StockShortfall audit
        // event rather than a negative balance or a fabricated fulfilment.
        affected.Should().Be(0);
        (await StockOfAsync(variantId)).Should().Be(3);
    }

    [Fact]
    public async Task Stock_can_be_drained_to_exactly_zero_but_not_below()
    {
        var variantId = await CreateManagedVariantAsync(2);

        await using (var db = _fixture.CreateDbContext())
        {
            (await ConsumeAsync(db, variantId, 2)).Should().Be(1);
            (await ConsumeAsync(db, variantId, 1)).Should().Be(0, "an empty variant cannot be consumed again");
        }

        (await StockOfAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task The_database_rejects_a_negative_balance_even_if_application_logic_is_bypassed()
    {
        var variantId = await CreateManagedVariantAsync(1);

        await using var db = _fixture.CreateDbContext();
        var write = async () => await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE dbo.ProductVariants SET StockQuantity = -1 WHERE Id = {variantId}");

        // Raw SQL surfaces the provider exception directly; 547 is SQL Server's constraint violation.
        var thrown = await write.Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();
        thrown.Which.Number.Should().Be(547);
        thrown.Which.Message.Should().Contain("CK_ProductVariants_StockQuantity_NonNegative");

        (await StockOfAsync(variantId)).Should().Be(1, "the rejected write must leave stock intact");
    }

    [Fact]
    public async Task Two_concurrent_consumptions_of_the_last_unit_produce_exactly_one_winner()
    {
        var variantId = await CreateManagedVariantAsync(1);

        // Separate DbContexts and connections: a genuine race, not an artificially serialised one.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 2).Select(async _ =>
            {
                await using var db = _fixture.CreateDbContext();
                return await ConsumeAsync(db, variantId, 1);
            }));

        results.Count(x => x == 1).Should().Be(1, "exactly one order may consume the final unit");
        results.Count(x => x == 0).Should().Be(1, "the loser must fall through to StockShortfall handling");
        (await StockOfAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task High_contention_never_oversells()
    {
        const int initial = 10;
        var variantId = await CreateManagedVariantAsync(initial);

        // 25 concurrent single-unit consumptions against 10 units.
        var results = await Task.WhenAll(
            Enumerable.Range(0, 25).Select(async _ =>
            {
                await using var db = _fixture.CreateDbContext();
                return await ConsumeAsync(db, variantId, 1);
            }));

        results.Count(x => x == 1).Should().Be(initial, "successes must equal the available units");
        (await StockOfAsync(variantId)).Should().Be(0);
    }

    [Fact]
    public async Task Replaying_the_same_consumption_would_decrement_again_which_is_why_the_paid_guard_matters()
    {
        // Documents the boundary of responsibility: the SQL statement itself is not idempotent, so
        // exactly-once comes from CompletePaidOrderAsync returning early once PaymentStatus is Paid.
        // If that guard is ever removed this test's premise is what breaks.
        var variantId = await CreateManagedVariantAsync(5);

        await using (var db = _fixture.CreateDbContext())
        {
            (await ConsumeAsync(db, variantId, 2)).Should().Be(1);
            (await ConsumeAsync(db, variantId, 2)).Should().Be(1);
        }

        (await StockOfAsync(variantId)).Should().Be(1);
    }

    [Fact]
    public async Task A_rolled_back_transaction_leaves_stock_untouched()
    {
        var variantId = await CreateManagedVariantAsync(5);

        await using (var db = _fixture.CreateDbContext())
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            (await ConsumeAsync(db, variantId, 3)).Should().Be(1);
            await tx.RollbackAsync();
        }

        // The paid transition and the decrement share one transaction, so a failure cannot leave
        // stock consumed while the order is not paid.
        (await StockOfAsync(variantId)).Should().Be(5);
    }
}
