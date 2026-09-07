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
    public async Task Feed_exports_each_named_variant_with_rial_price_and_a_deep_link()
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
        oneMonth.CurrentPrice.Should().Be(900_000);
        oneMonth.OldPrice.Should().Be(1_000_000);
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
        missing.Products.Should().BeEmpty();
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
