using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Vitorize.Application.DTOs.Torob;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class TorobCatalogServiceTests
{
    [Fact]
    public async Task Feed_exports_each_named_variant_with_toman_price_and_a_deep_link()
    {
        await using var db = CreateDb();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "اشتراک", Slug = "subscriptions", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Title = "پلاس", Slug = "plus", IsActive = true,
            DeliveryType = (byte)DeliveryType.Manual, CurrencyType = (byte)CurrencyType.Toman,
            BasePrice = 100_000, ThumbnailImagePath = "uploads/products/plus.png", CreatedAt = DateTime.UtcNow
        };
        var available = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Title = "یک ماهه", IsActive = true,
            Price = 100_000, DiscountPrice = 90_000, StockMode = (byte)ProductVariantStockMode.Manual,
            StockQuantity = 2, IsDefault = true, CreatedAt = DateTime.UtcNow
        };
        var unavailable = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Title = "سه ماهه", IsActive = true,
            Price = 250_000, StockMode = (byte)ProductVariantStockMode.Manual,
            StockQuantity = 0, CreatedAt = DateTime.UtcNow
        };
        var redirected = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category,
            Title = "خارجی", Slug = "external", IsActive = true, RedirectUrl = "https://example.com",
            DeliveryType = (byte)DeliveryType.Manual, CurrencyType = (byte)CurrencyType.Toman,
            BasePrice = 1, ThumbnailImagePath = "uploads/products/external.png", CreatedAt = DateTime.UtcNow
        };
        db.AddRange(category, product, available, unavailable, redirected);
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetProductsAsync(new TorobProductsRequest
        {
            Page = 1,
            Sort = "date_added_desc"
        });

        result.ApiVersion.Should().Be("torob_api_v3");
        result.Total.Should().Be(2);
        result.Products.Should().HaveCount(2);
        var oneMonth = result.Products.Single(item => item.PageUnique == $"variant:{available.Id:D}");
        oneMonth.PageUrl.Should().Be($"https://vitorize.example/product/plus?variant={available.Id:D}");
        // Stored in Toman, exported in Toman: Torob compares the feed with the price on the product page.
        oneMonth.CurrentPrice.Should().Be(90_000);
        oneMonth.OldPrice.Should().Be(100_000);
        oneMonth.Guarantee.Should().BeNull();
        oneMonth.DateAdded.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+00:00$");
        oneMonth.DateUpdated.Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\+00:00$");
        oneMonth.Availability.Should().BeTrue();
        oneMonth.ImageLinks.Should().ContainSingle().Which.Should().Be("https://media.vitorize.example/uploads/products/plus.png");
        result.Products.Single(item => item.PageUnique == $"variant:{unavailable.Id:D}").Availability.Should().BeFalse();
    }

    [Fact]
    public async Task Direct_lookup_returns_only_the_requested_offer()
    {
        await using var db = CreateDb();
        var category = new Category { Id = Guid.NewGuid(), Title = "بازی", Slug = "games", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, Title = "بازی", Slug = "game",
            IsActive = true, DeliveryType = (byte)DeliveryType.Manual, CurrencyType = (byte)CurrencyType.Rial,
            BasePrice = 50_000, ThumbnailImagePath = "uploads/products/game.png", CreatedAt = DateTime.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Title = "پیش‌فرض", IsActive = true,
            Price = 50_000, StockMode = (byte)ProductVariantStockMode.Unlimited, IsDefault = true, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(category, product, variant);
        await db.SaveChangesAsync();

        var found = await CreateService(db).GetProductsAsync(new TorobProductsRequest
        {
            PageUniques = [$"variant:{variant.Id:D}"]
        });
        var missing = await CreateService(db).GetProductsAsync(new TorobProductsRequest
        {
            PageUrls = ["https://vitorize.example/product/missing"]
        });

        found.Products.Should().ContainSingle();
        found.Products[0].Availability.Should().BeTrue();
        // 50,000 Rial in the database -> 5,000 Toman on Torob.
        found.Products[0].CurrentPrice.Should().Be(5_000);
        missing.Products.Should().BeEmpty();
    }

    [Fact]
    public async Task Bare_product_url_returns_every_offer_and_a_variant_link_returns_one()
    {
        await using var db = CreateDb();
        var category = new Category { Id = Guid.NewGuid(), Title = "اشتراک", Slug = "subscriptions", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, Title = "پلاس", Slug = "plus", IsActive = true,
            DeliveryType = (byte)DeliveryType.Manual, CurrencyType = (byte)CurrencyType.Toman, BasePrice = 100_000,
            ThumbnailImagePath = "uploads/products/plus.png", CreatedAt = DateTime.UtcNow
        };
        var oneMonth = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Title = "یک ماهه", IsActive = true,
            Price = 100_000, StockMode = (byte)ProductVariantStockMode.Unlimited, IsDefault = true, CreatedAt = DateTime.UtcNow
        };
        var threeMonths = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Product = product, Title = "سه ماهه", IsActive = true,
            Price = 250_000, StockMode = (byte)ProductVariantStockMode.Unlimited, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(category, product, oneMonth, threeMonths);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        // The storefront's canonical URL has no variant query; Torob asks for exactly that.
        var bare = await service.GetProductsAsync(new TorobProductsRequest { PageUrls = ["https://vitorize.example/product/plus"] });
        var slash = await service.GetProductsAsync(new TorobProductsRequest { PageUrls = ["https://www.vitorize.example/product/plus/"] });
        var tracked = await service.GetProductsAsync(new TorobProductsRequest { PageUrls = ["https://vitorize.example/product/plus?utm_source=torob"] });
        var deepLink = await service.GetProductsAsync(new TorobProductsRequest
        {
            PageUrls = [$"http://vitorize.example/product/plus?variant={oneMonth.Id:D}&utm_source=torob"]
        });

        bare.Products.Should().HaveCount(2);
        bare.Total.Should().Be(2);
        slash.Products.Should().HaveCount(2);
        tracked.Products.Should().HaveCount(2);
        deepLink.Products.Should().ContainSingle().Which.PageUnique.Should().Be($"variant:{oneMonth.Id:D}");
    }

    [Fact]
    public async Task Cursor_continues_after_its_anchor_product_is_deleted()
    {
        await using var db = CreateDb();
        var category = new Category { Id = Guid.NewGuid(), Title = "Test", Slug = "test", IsActive = true };
        db.Add(category);
        for (var i = 1; i <= 101; i++)
            db.Add(new Product
            {
                Id = Guid.Parse(i.ToString("x32")), CategoryId = category.Id, Category = category,
                Title = $"Offer {i}", Slug = $"offer-{i}", IsActive = true,
                ThumbnailImagePath = "uploads/test.png", CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var first = await service.GetProductsAsync(new TorobProductsRequest { Sort = "product_id_desc" });
        first.Products.Should().HaveCount(100);
        first.NextCursor.Should().NotBeNull();

        var anchor = await db.Products.SingleAsync(p => p.Id == Guid.Parse(2.ToString("x32")));
        anchor.IsDeleted = true;
        await db.SaveChangesAsync();
        var second = await service.GetProductsAsync(new TorobProductsRequest { Sort = "product_id_desc", Cursor = first.NextCursor });
        second.CurrentPage.Should().Be(2);
        second.Products.Should().ContainSingle().Which.PageUnique.Should().Be($"product:{Guid.Parse(1.ToString("x32")):D}");
        second.NextCursor.Should().BeNull();
    }

    private static VitorizeDbContext CreateDb() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options);

    private static TorobCatalogService CreateService(VitorizeDbContext db) => new(db,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Torob:StorefrontBaseUrl"] = "https://vitorize.example",
            ["Torob:MediaBaseUrl"] = "https://media.vitorize.example"
        }).Build());
}
