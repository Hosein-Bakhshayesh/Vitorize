using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// F1/F3 remediation: inventory is SKU-scoped, so a purchasable non-Instant product must always
/// own an active ProductVariant. Before this, a "variantless" Manual/SupportRequired product
/// aggregated to AvailableStock = 0 and was permanently unsellable while cart validation and
/// paid-time consumption silently skipped it.
///
/// These tests exercise the invariant against real SQL Server rather than the QA fixture, so the
/// seed can never mask the defect again (F8).
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class DefaultVariantInvariantIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public DefaultVariantInvariantIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    /// <summary>Marker title shared by AdminProductService and the V0021 migration.</summary>
    private const string DefaultVariantTitle = "پیش‌فرض";

    private async Task<Guid> CreateProductWithoutVariantsAsync(DeliveryType delivery)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "inv-cat", Slug = $"inv-cat-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "variantless",
            Slug = $"variantless-{Guid.NewGuid():N}", DeliveryType = (byte)delivery,
            BasePrice = 1000m, CurrencyType = (byte)CurrencyType.Toman, IsActive = true,
            MinOrderQuantity = 1, CreatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    /// <summary>Runs the shipped V0021 statement, so the test proves the real migration logic.</summary>
    private async Task RunDefaultVariantBackfillAsync()
    {
        var script = await File.ReadAllTextAsync(Path.Combine(
            _fixture.RepositoryRoot, "Database", "Versioned",
            "V0021__default_variants_for_managed_products.sql"));

        await using var db = _fixture.CreateDbContext();
        foreach (var batch in script.Split("\nGO", StringSplitOptions.RemoveEmptyEntries))
        {
            var sql = batch.Trim();
            if (sql.Length > 0) await db.Database.ExecuteSqlRawAsync(sql);
        }
    }

    private async Task<List<ProductVariant>> VariantsOfAsync(Guid productId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ProductVariants.AsNoTracking()
            .Where(v => v.ProductId == productId).ToListAsync();
    }

    [Theory]
    [InlineData(DeliveryType.Manual)]
    [InlineData(DeliveryType.SupportRequired)]
    public async Task Variantless_managed_product_gets_a_default_variant_and_is_sellable_after_stock_is_set(
        DeliveryType delivery)
    {
        var productId = await CreateProductWithoutVariantsAsync(delivery);
        (await VariantsOfAsync(productId)).Should().BeEmpty("the product starts in the broken pre-V0021 shape");

        await RunDefaultVariantBackfillAsync();

        var variants = await VariantsOfAsync(productId);
        variants.Should().ContainSingle("exactly one implicit SKU is created");
        var sku = variants[0];
        sku.Title.Should().Be(DefaultVariantTitle);
        sku.IsDefault.Should().BeTrue();
        sku.IsActive.Should().BeTrue();
        sku.StockMode.Should().Be((byte)ProductVariantStockMode.Manual);
        sku.StockQuantity.Should().Be(0, "unknown legacy stock must never become sellable automatically");

        // Still unavailable until an administrator sets real stock...
        ProductAvailabilityRules.IsInStock((byte)delivery, availableGiftCodes: 0, stockQuantity: sku.StockQuantity)
            .Should().BeFalse();

        await using (var db = _fixture.CreateDbContext())
        {
            var tracked = await db.ProductVariants.SingleAsync(v => v.Id == sku.Id);
            tracked.StockQuantity = 5;
            await db.SaveChangesAsync();
        }

        // ...and sellable once they do. This is the exact transition that was impossible before.
        var restocked = (await VariantsOfAsync(productId))[0];
        ProductAvailabilityRules.IsInStock((byte)delivery, 0, restocked.StockQuantity).Should().BeTrue();
        ProductAvailabilityRules.CanSatisfy((byte)delivery, 0, restocked.StockQuantity, requested: 5).Should().BeTrue();
        ProductAvailabilityRules.CanSatisfy((byte)delivery, 0, restocked.StockQuantity, requested: 6).Should().BeFalse();
    }

    [Fact]
    public async Task The_backfill_never_creates_a_second_variant_for_a_product_that_already_has_one()
    {
        var productId = await CreateProductWithoutVariantsAsync(DeliveryType.Manual);
        await RunDefaultVariantBackfillAsync();
        var first = await VariantsOfAsync(productId);
        first.Should().ContainSingle();

        // Idempotency: the ledger normally prevents re-running, but a repeated execution must
        // still not duplicate SKUs.
        await RunDefaultVariantBackfillAsync();
        var second = await VariantsOfAsync(productId);
        second.Should().ContainSingle();
        second[0].Id.Should().Be(first[0].Id, "the original SKU is preserved, not replaced");
    }

    [Fact]
    public async Task A_product_with_real_variants_is_left_completely_untouched()
    {
        var productId = await CreateProductWithoutVariantsAsync(DeliveryType.Manual);
        Guid realVariantId;
        await using (var db = _fixture.CreateDbContext())
        {
            var real = new ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = productId, Title = "نسخه واقعی", Price = 2500m,
                StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 42,
                IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
            };
            db.ProductVariants.Add(real);
            await db.SaveChangesAsync();
            realVariantId = real.Id;
        }

        await RunDefaultVariantBackfillAsync();

        var variants = await VariantsOfAsync(productId);
        variants.Should().ContainSingle("no implicit SKU is added alongside a real one");
        variants[0].Id.Should().Be(realVariantId);
        variants[0].StockQuantity.Should().Be(42, "existing stock is preserved");
        variants[0].Title.Should().Be("نسخه واقعی");
    }

    [Fact]
    public async Task Instant_products_are_deliberately_excluded_from_the_backfill()
    {
        var productId = await CreateProductWithoutVariantsAsync(DeliveryType.Instant);

        await RunDefaultVariantBackfillAsync();

        (await VariantsOfAsync(productId)).Should().BeEmpty(
            "Instant availability is the gift-code pool; adding a managed SKU would misrepresent it");
    }

    [Fact]
    public async Task Multiple_variants_keep_independent_inventory()
    {
        var productId = await CreateProductWithoutVariantsAsync(DeliveryType.Manual);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ProductVariants.AddRange(
                new ProductVariant
                {
                    Id = Guid.NewGuid(), ProductId = productId, Title = "A", Price = 1000m,
                    StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 0,
                    IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
                },
                new ProductVariant
                {
                    Id = Guid.NewGuid(), ProductId = productId, Title = "B", Price = 1000m,
                    StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 5,
                    IsDefault = false, IsActive = true, SortOrder = 1, CreatedAt = DateTime.UtcNow
                });
            await db.SaveChangesAsync();
        }

        var variants = (await VariantsOfAsync(productId)).OrderBy(v => v.SortOrder).ToList();
        ProductAvailabilityRules.IsInStock((byte)DeliveryType.Manual, 0, variants[0].StockQuantity)
            .Should().BeFalse("selecting the empty SKU must block purchase");
        ProductAvailabilityRules.IsInStock((byte)DeliveryType.Manual, 0, variants[1].StockQuantity)
            .Should().BeTrue("the stocked SKU stays purchasable");
    }
}
