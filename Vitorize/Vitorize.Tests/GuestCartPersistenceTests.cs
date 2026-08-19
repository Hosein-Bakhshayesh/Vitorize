using System.Text;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class GuestCartPersistenceTests
{
    [Fact]
    public void Guest_token_is_opaque_and_only_its_sha256_hash_is_suitable_for_storage()
    {
        var token = GuestCartToken.Create();
        var hash = GuestCartToken.Hash(token);

        Assert.True(GuestCartToken.IsWellFormed(token));
        Assert.Equal(43, token.Length);
        Assert.Equal(64, hash.Length);
        Assert.NotEqual(token, hash);
        Assert.Equal(hash, GuestCartToken.Hash(token));
        Assert.False(GuestCartToken.IsWellFormed("not-a-capability"));
    }

    [Fact]
    public async Task Guest_cart_is_persisted_by_hash_and_cannot_be_read_with_another_capability()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, "guest");
        var token = GuestCartToken.Create();
        var identity = CartIdentity.ForGuest(GuestCartToken.Hash(token));
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        var result = await service.AddItemAsync(identity, new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        var stored = await db.Carts.SingleAsync();

        Assert.Equal(2, result.TotalQuantity);
        Assert.Null(stored.UserId);
        Assert.Equal(GuestCartToken.Hash(token), stored.GuestTokenHash);
        Assert.NotNull(stored.LastActivityAt);
        var other = await service.GetAsync(CartIdentity.ForGuest(GuestCartToken.Hash(GuestCartToken.Create())));
        Assert.Empty(other.Items);
    }

    [Fact]
    public async Task Guest_merge_is_atomic_at_service_level_and_coalesces_identical_lines()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, "merge");
        var guestToken = GuestCartToken.Create();
        var guest = CartIdentity.ForGuest(GuestCartToken.Hash(guestToken));
        var userId = Guid.NewGuid();
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));

        await service.AddItemAsync(guest, new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        await service.AddItemAsync(CartIdentity.ForUser(userId), new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        var merged = await service.MergeGuestCartAsync(userId, guestToken);

        Assert.Single(merged.Items);
        Assert.Equal(3, merged.TotalQuantity);
        Assert.Single(await db.Carts.Where(x => x.UserId == userId).ToListAsync());
        Assert.Empty(await db.Carts.Where(x => x.GuestTokenHash == GuestCartToken.Hash(guestToken)).ToListAsync());
    }

    [Fact]
    public async Task Guest_merge_without_an_existing_user_cart_transfers_ownership_and_preserves_cart_id()
    {
        await using var db = CreateDb();
        var product = await SeedProductAsync(db, "transfer");
        var guestToken = GuestCartToken.Create();
        var guest = CartIdentity.ForGuest(GuestCartToken.Hash(guestToken));
        var service = new CartService(db, new TestEncryption(), new VatSettingsProvider(db));
        await service.AddItemAsync(guest, new AddToCartRequestDto { ProductId = product.Id, Quantity = 2 });
        var originalCartId = await db.Carts.Select(x => x.Id).SingleAsync();

        var userId = Guid.NewGuid();
        var merged = await service.MergeGuestCartAsync(userId, guestToken);
        var stored = await db.Carts.Include(x => x.CartItems).SingleAsync();

        Assert.Equal(originalCartId, merged.Id);
        Assert.Equal(originalCartId, stored.Id);
        Assert.Equal(userId, stored.UserId);
        Assert.Null(stored.GuestTokenHash);
        Assert.Null(stored.LastActivityAt);
        Assert.Equal(2, stored.CartItems.Single().Quantity);
    }

    private static VitorizeDbContext CreateDb() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static async Task<Product> SeedProductAsync(VitorizeDbContext db, string suffix)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Guest cart", Slug = $"guest-cart-{suffix}-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), Category = category, CategoryId = category.Id, Title = "Guest product",
            Slug = $"guest-product-{suffix}-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2,
            BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        // Inventory is SKU-scoped, so every purchasable non-Instant product owns a canonical
        // variant. Seeding one here keeps the fixture in the only shape the system can produce.
        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = 100m,
            StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 1000,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        db.AddRange(category, product);
        await db.SaveChangesAsync();
        return product;
    }

    private sealed class TestEncryption : IEncryptionService
    {
        public string Encrypt(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Decrypt(string encryptedValue) => Encoding.UTF8.GetString(Convert.FromBase64String(encryptedValue));
    }
}
