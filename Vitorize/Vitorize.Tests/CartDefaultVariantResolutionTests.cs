using System.Text;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// F3 remediation. Cart validation, checkout revalidation and paid-time stock consumption are all
/// keyed on CartItem.ProductVariantId, so a managed-stock line whose variant id stayed null was
/// silently skipped by every one of them. AddItemAsync now resolves the product's single SKU
/// server-side instead of trusting the caller to send one.
/// </summary>
public sealed class CartDefaultVariantResolutionTests
{
    private static VitorizeDbContext CreateDb() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase($"cart-variant-{Guid.NewGuid():N}").Options);

    private static CartService NewService(VitorizeDbContext db) =>
        new(db, new TestEncryption(), new VatSettingsProvider(db));

    private static Product SeedProduct(VitorizeDbContext db, DeliveryType delivery,
        params (string Title, int Stock, bool IsDefault)[] variants)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "cat", Slug = $"cat-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = "p",
            Slug = $"p-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)delivery,
            BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var order = 0;
        foreach (var (title, stock, isDefault) in variants)
        {
            product.ProductVariants.Add(new ProductVariant
            {
                Id = Guid.NewGuid(), ProductId = product.Id, Title = title, Price = 100m,
                StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = stock,
                IsDefault = isDefault, IsActive = true, SortOrder = order++, CreatedAt = DateTime.UtcNow
            });
        }
        db.AddRange(category, product);
        db.SaveChanges();
        return product;
    }

    [Theory]
    [InlineData(DeliveryType.Manual)]
    [InlineData(DeliveryType.SupportRequired)]
    public async Task A_request_without_a_variant_id_binds_the_line_to_the_products_single_sku(
        DeliveryType delivery)
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, delivery, ("پیش‌فرض", 10, true));

        await NewService(db).AddItemAsync(CartIdentity.ForUser(Guid.NewGuid()),
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });

        var line = await db.CartItems.SingleAsync();
        Assert.Equal(product.ProductVariants.Single().Id, line.ProductVariantId);
    }

    [Fact]
    public async Task Managed_stock_is_enforced_even_though_the_caller_sent_no_variant_id()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, DeliveryType.Manual, ("پیش‌فرض", 3, true));

        var tooMany = await Assert.ThrowsAsync<BusinessException>(() =>
            NewService(db).AddItemAsync(CartIdentity.ForUser(Guid.NewGuid()),
                new AddToCartRequestDto { ProductId = product.Id, Quantity = 4 }));

        Assert.NotNull(tooMany.Message);
        Assert.Empty(await db.CartItems.ToListAsync());
    }

    [Fact]
    public async Task Repeated_adds_accumulate_against_the_same_resolved_sku_rather_than_splitting()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, DeliveryType.Manual, ("پیش‌فرض", 10, true));
        var identity = CartIdentity.ForUser(Guid.NewGuid());
        var service = NewService(db);

        await service.AddItemAsync(identity, new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        await service.AddItemAsync(identity, new AddToCartRequestDto { ProductId = product.Id, Quantity = 3 });

        var line = await db.CartItems.SingleAsync();
        Assert.Equal(5, line.Quantity);

        // The accumulated quantity is what the stock ceiling is measured against.
        await Assert.ThrowsAsync<BusinessException>(() =>
            service.AddItemAsync(identity, new AddToCartRequestDto { ProductId = product.Id, Quantity = 6 }));
    }

    [Fact]
    public async Task A_multi_variant_product_still_requires_the_customer_to_choose()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, DeliveryType.Manual, ("A", 10, true), ("B", 10, false));

        await Assert.ThrowsAsync<BusinessException>(() =>
            NewService(db).AddItemAsync(CartIdentity.ForUser(Guid.NewGuid()),
                new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 }));
    }

    [Fact]
    public async Task A_managed_product_with_no_sku_at_all_is_refused_instead_of_selling_unlimited_stock()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, DeliveryType.Manual);

        // This is the exact pre-fix defect: the line used to be accepted with a null variant id and
        // then bypassed checkout validation and paid-time consumption entirely.
        await Assert.ThrowsAsync<BusinessException>(() =>
            NewService(db).AddItemAsync(CartIdentity.ForUser(Guid.NewGuid()),
                new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 }));
        Assert.Empty(await db.CartItems.ToListAsync());
    }

    [Fact]
    public async Task Instant_products_remain_variant_optional_because_their_stock_is_the_gift_code_pool()
    {
        await using var db = CreateDb();
        var product = SeedProduct(db, DeliveryType.Instant);
        db.GiftCodes.AddRange(
            new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = "a", MaskedCode = "***a", Status = (byte)GiftCodeStatus.Available, CreatedAt = DateTime.UtcNow },
            new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = "b", MaskedCode = "***b", Status = (byte)GiftCodeStatus.Available, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        await NewService(db).AddItemAsync(CartIdentity.ForUser(Guid.NewGuid()),
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });

        var line = await db.CartItems.SingleAsync();
        Assert.Null(line.ProductVariantId);
    }

    private sealed class TestEncryption : IEncryptionService
    {
        public string Encrypt(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Decrypt(string encryptedValue) => Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
    }
}
