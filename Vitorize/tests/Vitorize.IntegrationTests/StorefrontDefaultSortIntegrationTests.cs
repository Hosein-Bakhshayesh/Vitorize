using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Products;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Storefront;

namespace Vitorize.IntegrationTests;

/// <summary>
/// The administrator chooses one default order for the storefront; a customer who asks for a
/// different order still gets it. These run against real SQL Server because the interesting part is
/// the ORDER BY the database actually executes - particularly "available first", which has to agree
/// with the canonical availability rules across every inventory model Vitorize supports rather than
/// with a bare StockQuantity comparison.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class StorefrontDefaultSortIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public StorefrontDefaultSortIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    private sealed record Catalogue(Guid CategoryId, Guid SecondCategoryId, Dictionary<string, Guid> Products);

    /// <summary>
    /// One product per inventory shape, in an isolated category so other fixtures cannot affect the
    /// order. Creation timestamps are spread deliberately so every ordering has a stable expectation.
    /// </summary>
    private async Task<Catalogue> SeedCatalogueAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var now = DateTime.UtcNow;

        var category = new Category
        {
            Id = Guid.NewGuid(), Title = $"sort-cat-{suffix}", Slug = $"sort-cat-{suffix}",
            IsActive = true, CreatedAt = now
        };
        var second = new Category
        {
            Id = Guid.NewGuid(), Title = $"sort-cat2-{suffix}", Slug = $"sort-cat2-{suffix}",
            IsActive = true, CreatedAt = now
        };
        db.Categories.AddRange(category, second);

        var ids = new Dictionary<string, Guid>();
        Product Make(string label, DeliveryType delivery, decimal price, int ageDays, bool forceOutOfStock = false)
        {
            var product = new Product
            {
                Id = Guid.NewGuid(), CategoryId = category.Id, Title = $"SORT-{label}-{suffix}",
                Slug = $"sort-{label.ToLowerInvariant()}-{suffix}", DeliveryType = (byte)delivery,
                ProductType = 1, BasePrice = price, CurrencyType = (byte)CurrencyType.Toman,
                IsActive = true, IsDeleted = false, ForceOutOfStock = forceOutOfStock,
                MinOrderQuantity = 1, SortOrder = 0, CreatedAt = now.AddDays(-ageDays)
            };
            ids[label] = product.Id;
            db.Products.Add(product);
            return product;
        }

        void Sku(Product product, ProductVariantStockMode mode, int quantity) =>
            db.ProductVariants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = product.BasePrice,
                StockMode = (byte)mode, StockQuantity = quantity, IsDefault = true, IsActive = true,
                SortOrder = 0, CreatedAt = now
            });

        void GiftCode(Product product, GiftCodeStatus status) =>
            db.GiftCodes.Add(new GiftCode
            {
                Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = $"SORT-{Guid.NewGuid():N}",
                MaskedCode = "****", Status = (byte)status, EncryptionVersion = 0,
                CodeHashFingerprint = $"sort-{Guid.NewGuid():N}", CreatedAt = now
            });

        // A: managed stock, available.            C: unlimited.            E: Instant with a code.
        // B: stock but overridden out of stock.   D: managed, zero stock.  F: Instant, pool empty.
        var a = Make("A", DeliveryType.Manual, 500m, 5);
        var b = Make("B", DeliveryType.Manual, 400m, 4, forceOutOfStock: true);
        var c = Make("C", DeliveryType.Manual, 300m, 3);
        var d = Make("D", DeliveryType.Manual, 200m, 2);
        var e = Make("E", DeliveryType.Instant, 100m, 1);
        var f = Make("F", DeliveryType.Instant, 600m, 6);

        Sku(a, ProductVariantStockMode.Manual, 25);
        Sku(b, ProductVariantStockMode.Manual, 25);
        Sku(c, ProductVariantStockMode.Unlimited, 0);
        Sku(d, ProductVariantStockMode.Manual, 0);
        GiftCode(e, GiftCodeStatus.Available);
        GiftCode(f, GiftCodeStatus.Sold);

        // F also sits in a second category, so a multi-category listing must not repeat it.
        db.ProductCategories.Add(new ProductCategory { ProductId = f.Id, CategoryId = second.Id, CreatedAt = now });

        await db.SaveChangesAsync();
        return new Catalogue(category.Id, second.Id, ids);
    }

    private async Task SetDefaultSortAsync(string? value)
    {
        await using var db = _fixture.CreateDbContext();
        var existing = await db.Settings.FirstOrDefaultAsync(x => x.Key == StorefrontProductSortModes.SettingKey);
        if (value is null)
        {
            if (existing is not null) db.Settings.Remove(existing);
        }
        else if (existing is null)
        {
            db.Settings.Add(new Setting
            {
                Id = Guid.NewGuid(), Key = StorefrontProductSortModes.SettingKey, Value = value,
                GroupName = "General", ValueType = "string", UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
    }

    /// <summary>The seeded products, in the order the storefront returns them.</summary>
    private async Task<List<string>> ListAsync(Catalogue catalogue, string? sort = null, Guid? categoryId = null)
    {
        var client = _fixture.CreateClient();
        var url = $"/api/products?page=1&pageSize=200&categoryId={categoryId ?? catalogue.CategoryId}";
        if (sort is not null) url += $"&sort={sort}";

        var response = await client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<ProductListItemDto>>>();
        payload!.IsSuccess.Should().BeTrue();

        return payload.Data!.Items
            .Where(x => x.Title.StartsWith("SORT-", StringComparison.Ordinal))
            .Select(x => x.Title.Split('-')[1])
            .ToList();
    }

    private static void AvailableFirst(IReadOnlyList<string> order)
    {
        order.Should().HaveCount(6);
        // A, C and E are purchasable; B, D and F are not. Membership of the two halves is what the
        // rule guarantees - the order inside each half is asserted separately.
        order.Take(3).Should().BeEquivalentTo(new[] { "A", "C", "E" });
        order.Skip(3).Should().BeEquivalentTo(new[] { "B", "D", "F" });
    }

    [Fact]
    public async Task A_missing_setting_orders_available_products_first()
    {
        var catalogue = await SeedCatalogueAsync();
        await SetDefaultSortAsync(null);

        AvailableFirst(await ListAsync(catalogue));
    }

    [Fact]
    public async Task An_unusable_setting_value_falls_back_instead_of_ordering_arbitrarily()
    {
        var catalogue = await SeedCatalogueAsync();
        await SetDefaultSortAsync("NoSuchMode");

        AvailableFirst(await ListAsync(catalogue));
    }

    [Fact]
    public async Task Availability_first_respects_every_inventory_model_and_is_deterministic()
    {
        var catalogue = await SeedCatalogueAsync();
        await SetDefaultSortAsync("AvailabilityFirst");

        var first = await ListAsync(catalogue);
        AvailableFirst(first);

        // Equal products must not shuffle between requests: the secondary order is SortOrder, then
        // newest, then id. All six share SortOrder 0, so within each half it is newest-first.
        first.Take(3).Should().ContainInOrder("E", "C", "A");
        first.Skip(3).Should().ContainInOrder("D", "B", "F");
        (await ListAsync(catalogue)).Should().Equal(first);
    }

    [Fact]
    public async Task Newest_and_oldest_order_by_creation_time()
    {
        var catalogue = await SeedCatalogueAsync();

        await SetDefaultSortAsync("Newest");
        (await ListAsync(catalogue)).Should().Equal("E", "D", "C", "B", "A", "F");

        await SetDefaultSortAsync("Oldest");
        (await ListAsync(catalogue)).Should().Equal("F", "A", "B", "C", "D", "E");
    }

    [Fact]
    public async Task Price_orders_run_in_both_directions()
    {
        var catalogue = await SeedCatalogueAsync();

        await SetDefaultSortAsync("PriceLowToHigh");
        (await ListAsync(catalogue)).Should().Equal("E", "D", "C", "B", "A", "F");

        await SetDefaultSortAsync("PriceHighToLow");
        (await ListAsync(catalogue)).Should().Equal("F", "A", "B", "C", "D", "E");
    }

    [Fact]
    public async Task Best_selling_uses_paid_order_quantity()
    {
        var catalogue = await SeedCatalogueAsync();
        var (user, _) = await _fixture.CreateUserAndTokenAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            // D sells more than A; a cancelled order for C must not count towards anything.
            var paid = NewOrder(user.Id, PaymentStatus.Paid);
            var unpaid = NewOrder(user.Id, PaymentStatus.Pending);
            db.Orders.AddRange(paid, unpaid);
            db.OrderItems.AddRange(
                NewItem(paid.Id, catalogue.Products["D"], "D", 7),
                NewItem(paid.Id, catalogue.Products["A"], "A", 3),
                NewItem(unpaid.Id, catalogue.Products["C"], "C", 99));
            await db.SaveChangesAsync();
        }

        await SetDefaultSortAsync("BestSelling");
        var order = await ListAsync(catalogue);

        order.Should().HaveCount(6);
        order[0].Should().Be("D");
        order[1].Should().Be("A");
        // C's unpaid order is not a sale, so it stays with the products that have never sold.
        order.Skip(2).Should().BeEquivalentTo(new[] { "B", "C", "E", "F" });
    }

    [Fact]
    public async Task An_explicit_customer_sort_overrides_the_administrator_default()
    {
        var catalogue = await SeedCatalogueAsync();
        await SetDefaultSortAsync("AvailabilityFirst");

        // The customer asks for cheapest; the saved default must not win over that.
        (await ListAsync(catalogue, sort: "cheapest")).Should().Equal("E", "D", "C", "B", "A", "F");

        // Dropping the explicit choice returns them to the configured default.
        AvailableFirst(await ListAsync(catalogue));
    }

    [Fact]
    public async Task A_saved_change_takes_effect_on_the_very_next_listing()
    {
        var catalogue = await SeedCatalogueAsync();

        await SetDefaultSortAsync("AvailabilityFirst");
        AvailableFirst(await ListAsync(catalogue));

        // No restart, no cache flush, no new client: the next request already follows the new value.
        await SetDefaultSortAsync("Oldest");
        (await ListAsync(catalogue)).Should().Equal("F", "A", "B", "C", "D", "E");

        await SetDefaultSortAsync("AvailabilityFirst");
        AvailableFirst(await ListAsync(catalogue));
    }

    [Fact]
    public async Task A_category_listing_follows_the_default_and_never_repeats_a_product()
    {
        var catalogue = await SeedCatalogueAsync();
        await SetDefaultSortAsync("Newest");

        (await ListAsync(catalogue)).Should().Equal("E", "D", "C", "B", "A", "F");

        // F belongs to two categories; asking for the second must return it exactly once.
        var secondary = await ListAsync(catalogue, categoryId: catalogue.SecondCategoryId);
        secondary.Should().Equal("F");
        secondary.Should().OnlyHaveUniqueItems();
    }

    // ---------------------------------------------------------------- the setting is a fixed set

    [Theory]
    [InlineData("Popular")]                 // deliberately unsupported: no popularity metric exists
    [InlineData("'; DROP TABLE Settings--")]
    [InlineData("")]
    public async Task An_unsupported_ordering_is_rejected_rather_than_silently_ignored(string value)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        await SetDefaultSortAsync("Newest");

        var response = await admin.PostAsJsonAsync("/api/admin/settings", new
        {
            Key = StorefrontProductSortModes.SettingKey, Value = value,
            GroupName = "General", ValueType = "sortmode", Description = "default order"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest,
            "the query layer would fall back to the default, so a bad value would look accepted and do nothing");

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Settings.AsNoTracking()
            .Where(x => x.Key == StorefrontProductSortModes.SettingKey)
            .Select(x => x.Value).FirstOrDefaultAsync();
        stored.Should().Be("Newest", "a rejected save must leave the previous choice intact");
    }

    [Fact]
    public async Task A_differently_cased_code_is_stored_canonically()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        // Accepted, because codes are matched case-insensitively - but written back in the canonical
        // spelling so the admin select can preselect it.
        (await admin.PostAsJsonAsync("/api/admin/settings", new
        {
            Key = StorefrontProductSortModes.SettingKey, Value = "newest",
            GroupName = "General", ValueType = "sortmode", Description = "default order"
        })).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        (await db.Settings.AsNoTracking()
            .Where(x => x.Key == StorefrontProductSortModes.SettingKey)
            .Select(x => x.Value).FirstOrDefaultAsync())
            .Should().Be("Newest");
    }

    [Theory]
    [InlineData("AvailabilityFirst")]
    [InlineData("BestSelling")]
    [InlineData("Newest")]
    [InlineData("Oldest")]
    [InlineData("PriceLowToHigh")]
    [InlineData("PriceHighToLow")]
    [InlineData("MostDiscounted")]
    public async Task Every_supported_mode_is_accepted_and_stored_verbatim(string code)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var admin = _fixture.CreateClient(token);

        var response = await admin.PostAsJsonAsync("/api/admin/settings", new
        {
            Key = StorefrontProductSortModes.SettingKey, Value = code,
            GroupName = "General", ValueType = "sortmode", Description = "default order"
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        await using var db = _fixture.CreateDbContext();
        var row = await db.Settings.AsNoTracking()
            .FirstAsync(x => x.Key == StorefrontProductSortModes.SettingKey);
        row.Value.Should().Be(code);
        row.ValueType.Should().Be("sortmode", "the admin UI renders a select from this type");
    }

    private static Order NewOrder(Guid userId, PaymentStatus payment) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"SORT-{Guid.NewGuid():N}"[..20],
        Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)payment,
        SubtotalAmount = 1000m, DiscountAmount = 0m, FinalAmount = 1000m,
        CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow,
        PaidAt = payment == PaymentStatus.Paid ? DateTime.UtcNow : null
    };

    private static OrderItem NewItem(Guid orderId, Guid productId, string title, int quantity) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, ProductId = productId, ProductTitle = title,
        Quantity = quantity, UnitPrice = 100m, TotalPrice = 100m * quantity,
        CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Manual,
        DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
    };
}
