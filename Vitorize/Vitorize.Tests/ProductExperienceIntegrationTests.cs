using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class ProductExperienceIntegrationTests
{
    [Fact]
    public async Task Public_product_detail_returns_only_active_metadata_and_sanitized_html()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db);
        product.FullDescription = "<h2>متن امن</h2><script>alert(1)</script>";
        product.ProductFeatures.Add(new ProductFeature { Id = Guid.NewGuid(), Title = "پلتفرم", Value = "PS5", IconKey = "gamepad-2", SortOrder = 20, IsActive = true });
        product.ProductFeatures.Add(new ProductFeature { Id = Guid.NewGuid(), Title = "مخفی", Value = "داخلی", SortOrder = 10, IsActive = false });
        product.ProductInputFields.Add(new ProductInputField
        {
            Id = Guid.NewGuid(), Key = "account_email", Label = "ایمیل حساب", FieldType = (byte)ProductInputFieldType.Email,
            IsRequired = true, DisplayStage = (byte)ProductInputStage.ProductPage, SortOrder = 10, IsActive = true,
            OptionsJson = JsonSerializer.Serialize(Array.Empty<string>()), CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var dto = await new ProductService(db, new StrictHtmlContentSanitizer()).GetProductBySlugAsync(product.Slug);

        Assert.Single(dto.Features);
        Assert.Equal("پلتفرم", dto.Features[0].Title);
        Assert.Equal("gamepad-2", dto.Features[0].IconKey);
        Assert.Single(dto.InputFields);
        Assert.DoesNotContain("script", dto.FullDescription!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Cart_does_not_merge_different_values_and_encrypts_sensitive_data()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db);
        product.ProductInputFields.Add(new ProductInputField
        {
            Id = Guid.NewGuid(), Key = "account_token", Label = "شناسه محرمانه", FieldType = (byte)ProductInputFieldType.Secret,
            IsRequired = true, IsSensitive = true, DisplayStage = 1, SortOrder = 10, IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        var userId = Guid.NewGuid();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        await service.AddItemAsync(userId, new AddToCartRequestDto { ProductId = product.Id, Quantity = 1, InputValues = new() { ["account_token"] = "secret-A" } });
        var cart = await service.AddItemAsync(userId, new AddToCartRequestDto { ProductId = product.Id, Quantity = 1, InputValues = new() { ["account_token"] = "secret-B" } });

        Assert.Equal(2, cart.Items.Count);
        Assert.Equal(2, await db.CartItems.Select(x => x.InputFingerprint).Distinct().CountAsync());
        var stored = await db.CartItemInputValues.ToListAsync();
        Assert.All(stored, x => Assert.Null(x.Value));
        Assert.All(stored, x => Assert.NotNull(x.EncryptedValue));
        Assert.DoesNotContain(stored, x => x.EncryptedValue is "secret-A" or "secret-B");
        Assert.All(cart.Items.SelectMany(x => x.InputValues), x => Assert.True(x.IsMasked));
    }

    [Fact]
    public async Task Product_filters_are_applied_before_counting_and_paging()
    {
        await using var db = CreateDb();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Catalog", Slug = $"catalog-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var matching = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = "Matching", Slug = $"matching-{Guid.NewGuid():N}",
            ProductType = 2, DeliveryType = 1, RequiresVerification = true, BasePrice = 100m, DiscountPrice = 70m,
            CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var excluded = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = "Excluded", Slug = $"excluded-{Guid.NewGuid():N}",
            ProductType = 2, DeliveryType = 2, RequiresVerification = true, BasePrice = 100m, DiscountPrice = 70m,
            CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.AddRange(category, matching, excluded);
        await db.SaveChangesAsync();

        var result = await new ProductService(db, new StrictHtmlContentSanitizer()).GetProductsAsync(new ProductFilterDto
        {
            ProductTypes = new List<byte> { 2 }, DeliveryType = 1, RequiresVerification = true,
            MinDiscountPercent = 25m, Page = 1, PageSize = 1
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal(matching.Id, result.Items[0].Id);
    }

    [Fact]
    public async Task Catalog_sort_uses_id_as_a_stable_tie_breaker()
    {
        await using var db = CreateDb();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Catalog", Slug = $"catalog-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var createdAt = DateTime.UtcNow;
        var laterId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var earlierId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        db.AddRange(
            new Product { Id = laterId, Category = category, CategoryId = category.Id, Title = "Later", Slug = $"later-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2, BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = createdAt },
            new Product { Id = earlierId, Category = category, CategoryId = category.Id, Title = "Earlier", Slug = $"earlier-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2, BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = createdAt });
        await db.SaveChangesAsync();

        var result = await new ProductService(db, new StrictHtmlContentSanitizer()).GetProductsAsync(new ProductFilterDto
        {
            Sort = "newest", Page = 1, PageSize = 20
        });

        Assert.Equal(new[] { earlierId, laterId }, result.Items.Select(x => x.Id));
    }

    private static VitorizeDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new VitorizeDbContext(options);
    }

    private static Product SeedProduct(VitorizeDbContext db)
    {
        var category = new Category { Id = Guid.NewGuid(), Title = "بازی", Slug = "games", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Category = category, Title = "محصول آزمون", Slug = $"test-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = 2, CurrencyType = 2, BasePrice = 1000, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category); db.Products.Add(product); return product;
    }

    private sealed class TestEncryption : IEncryptionService
    {
        public string Encrypt(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Decrypt(string encryptedValue) => Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
    }
}
