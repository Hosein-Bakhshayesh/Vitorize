using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

/// <summary>
/// The inventory policies exercised through the real cart → checkout → payment path, not through the
/// rule class alone. What matters here is what the database looks like afterwards: an unlimited SKU
/// must never be decremented, a counted one must be decremented exactly once, and the administrator's
/// availability override must stop a new purchase without touching a single unit of stock.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class InventoryPolicyIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public InventoryPolicyIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task An_unlimited_sku_sells_any_quantity_and_is_never_decremented(int quantity)
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Unlimited, stock: 0);
        await CreditWalletAsync(user.Id, 1_000_000m);

        await AddToCartAsync(user.Id, product.Id, quantity);
        var order = await CheckoutAsync(user.Id);

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(user.Id, order.OrderId);

        await using var verify = _fixture.CreateDbContext();
        var stored = await verify.Orders.SingleAsync(x => x.Id == order.OrderId);
        stored.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);

        var variant = await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id);
        // The point of the policy: a paid order leaves the quantity exactly where it was, so an
        // unlimited SKU can never count down to "out of stock".
        variant.StockQuantity.Should().Be(0);
        variant.StockMode.Should().Be((byte)ProductVariantStockMode.Unlimited);

        // And it is still available for the next customer.
        (await AvailabilityOfAsync(product.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task A_counted_sku_is_decremented_exactly_once_and_a_replayed_payment_changes_nothing()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Manual, stock: 5);
        await CreditWalletAsync(user.Id, 1_000_000m);

        await AddToCartAsync(user.Id, product.Id, quantity: 2);
        var order = await CheckoutAsync(user.Id);

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(user.Id, order.OrderId);

        await using (var afterFirst = _fixture.CreateDbContext())
            (await afterFirst.ProductVariants.SingleAsync(x => x.ProductId == product.Id))
                .StockQuantity.Should().Be(3, "5 - 2 units");

        // A replayed capture must be refused rather than consume a second time.
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var replay = () => scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(user.Id, order.OrderId);
            await replay.Should().ThrowAsync<BusinessException>();
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id))
            .StockQuantity.Should().Be(3, "the replay must not decrement again");
    }

    [Fact]
    public async Task A_forced_out_of_stock_product_cannot_be_added_to_a_cart_and_keeps_its_stock()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Manual, stock: 5, forceOutOfStock: true);

        var act = () => AddToCartAsync(user.Id, product.Id, quantity: 1);
        await act.Should().ThrowAsync<BusinessException>();

        await using var verify = _fixture.CreateDbContext();
        (await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id))
            .StockQuantity.Should().Be(5, "taking a product off sale must never destroy inventory");
        (await verify.CartItems.CountAsync(x => x.ProductId == product.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Forcing_a_product_out_of_stock_after_it_reached_a_cart_blocks_the_checkout()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Manual, stock: 5);

        // The line is added while the product is on sale, then an administrator withdraws it.
        await AddToCartAsync(user.Id, product.Id, quantity: 1);
        await SetForceOutOfStockAsync(product.Id, true);

        var act = () => CheckoutAsync(user.Id);
        await act.Should().ThrowAsync<BusinessException>();

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id))
            .StockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task An_unlimited_sku_that_is_forced_out_of_stock_is_unavailable_and_unsellable()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Unlimited, stock: 0, forceOutOfStock: true);

        (await AvailabilityOfAsync(product.Id)).Should().BeFalse("the override outranks Unlimited");

        var act = () => AddToCartAsync(user.Id, product.Id, quantity: 1);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Clearing_the_override_restores_availability_with_the_dormant_stock_intact()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Manual, stock: 5, forceOutOfStock: true);

        (await AvailabilityOfAsync(product.Id)).Should().BeFalse();

        await SetForceOutOfStockAsync(product.Id, false);

        (await AvailabilityOfAsync(product.Id)).Should().BeTrue("clearing the override returns the product to its own inventory rule");

        // And the product is immediately purchasable again with every one of its five units.
        var cart = await AddToCartAsync(user.Id, product.Id, quantity: 5);
        cart.Items.Single().Quantity.Should().Be(5);

        await using var verify = _fixture.CreateDbContext();
        (await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id))
            .StockQuantity.Should().Be(5);
    }

    [Fact]
    public async Task A_counted_sku_still_refuses_more_than_it_has()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedAsync(ProductVariantStockMode.Manual, stock: 2);

        var act = () => AddToCartAsync(user.Id, product.Id, quantity: 3);
        await act.Should().ThrowAsync<BusinessException>();

        // Unlimited is the only thing that lifts the ceiling; Manual keeps it.
        (await AddToCartAsync(user.Id, product.Id, quantity: 2)).Items.Single().Quantity.Should().Be(2);
    }

    // ---------------------------------------------------------------- helpers

    private CartService Cart(Vitorize.Infrastructure.Persistence.VitorizeDbContext db) =>
        new(db, _fixture.Factory.Services.GetRequiredService<IEncryptionService>(), new VatSettingsProvider(db));

    private async Task<CartDto> AddToCartAsync(Guid userId, Guid productId, int quantity)
    {
        await using var db = _fixture.CreateDbContext();
        return await Cart(db).AddItemAsync(userId, new AddToCartRequestDto
        {
            ProductId = productId, Quantity = quantity
        });
    }

    private async Task<CheckoutResultDto> CheckoutAsync(Guid userId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
            .CheckoutAsync(userId, new CheckoutRequestDto());
    }

    private async Task SetForceOutOfStockAsync(Guid productId, bool value)
    {
        await using var db = _fixture.CreateDbContext();
        var product = await db.Products.SingleAsync(x => x.Id == productId);
        product.ForceOutOfStock = value;
        await db.SaveChangesAsync();
    }

    /// <summary>Effective availability as the storefront computes it, straight from the canonical rule.</summary>
    private async Task<bool> AvailabilityOfAsync(Guid productId)
    {
        await using var db = _fixture.CreateDbContext();
        var row = await db.Products
            .Where(x => x.Id == productId)
            .Select(x => new
            {
                x.ForceOutOfStock,
                x.DeliveryType,
                Variant = x.ProductVariants.Where(v => v.IsActive)
                    .Select(v => new { v.StockMode, v.StockQuantity }).FirstOrDefault()
            })
            .SingleAsync();

        return Vitorize.Application.Common.ProductAvailabilityRules.IsAvailableForSale(
            row.ForceOutOfStock,
            row.DeliveryType,
            (ProductVariantStockMode)(row.Variant?.StockMode ?? (byte)ProductVariantStockMode.Manual),
            availableGiftCodes: 0,
            stockQuantity: row.Variant?.StockQuantity ?? 0);
    }

    private async Task CreditWalletAsync(Guid userId, decimal amount)
    {
        await using var db = _fixture.CreateDbContext();
        var wallet = await db.Wallets.FirstOrDefaultAsync(x => x.UserId == userId);
        if (wallet is null)
        {
            wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = 0, CreatedAt = DateTime.UtcNow };
            db.Wallets.Add(wallet);
        }
        wallet.Balance += amount;
        await db.SaveChangesAsync();
    }

    private async Task<Product> SeedAsync(
        ProductVariantStockMode stockMode, int stock, bool forceOutOfStock = false)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "policy", Slug = $"policy-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Policy product",
            Slug = $"policy-{Guid.NewGuid():N}", ProductType = 1,
            DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = 1_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, ForceOutOfStock = forceOutOfStock, CreatedAt = DateTime.UtcNow
        };
        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id,
            Title = Vitorize.Application.Common.ProductAvailabilityRules.DefaultVariantTitle,
            Price = product.BasePrice, StockMode = (byte)stockMode, StockQuantity = stock,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
