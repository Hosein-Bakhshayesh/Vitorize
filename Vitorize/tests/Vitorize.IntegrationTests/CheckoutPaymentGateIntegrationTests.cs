using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

/// <summary>
/// The money-side proof for the new checkout workflow, at service level rather than through the UI.
///
/// Payment always starts from an existing order, and an order cannot be created while a required
/// product field is missing. These tests measure that consequence directly: the gateway provider is
/// never called and the wallet is never debited. The coupon+wallet combination is covered here too,
/// which had no explicit service-level test before.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CheckoutPaymentGateIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public CheckoutPaymentGateIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task A_missing_required_field_means_the_gateway_provider_is_never_called()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(2_000m, ("player_id", true));
        await AddToCartAsync(user.Id, product.Id);

        var gateway = (FakeZarinpalGateway)_fixture.Factory.Services.GetRequiredService<IZarinpalGatewayService>();
        var before = gateway.CreatePaymentCalls;

        var act = () => CheckoutAsync(user.Id);
        await act.Should().ThrowAsync<BusinessException>();

        gateway.CreatePaymentCalls.Should().Be(before, "no order exists, so nothing may reach the provider");
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
        (await verify.Payments.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task A_missing_required_field_means_the_wallet_is_never_debited()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(2_000m, ("player_id", true));
        await CreditWalletAsync(user.Id, 100_000m);
        await AddToCartAsync(user.Id, product.Id);

        var balanceBefore = await WalletBalanceAsync(user.Id);
        var ledgerBefore = await WalletTransactionCountAsync(user.Id);

        var act = () => CheckoutAsync(user.Id);
        await act.Should().ThrowAsync<BusinessException>();

        (await WalletBalanceAsync(user.Id)).Should().Be(balanceBefore, "an unpayable cart must not move money");
        (await WalletTransactionCountAsync(user.Id)).Should().Be(ledgerBefore);
    }

    [Fact]
    public async Task Wallet_pays_a_coupon_discounted_order_exactly_once_and_inventory_follows_quantity()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(10_000m, ("player_id", true), stock: 5);
        var coupon = await SeedPercentCouponAsync(10);

        await CreditWalletAsync(user.Id, 100_000m);
        var cart = await AddToCartAsync(user.Id, product.Id, quantity: 2);
        await FillAsync(user.Id, cart.Items.Single().Id, quantity: 2, new() { ["player_id"] = "WALLET-COUPON" });

        var order = await CheckoutAsync(user.Id, coupon.Code);
        // 2 x 10,000 less 10% -> the discount must reach the amount actually charged.
        order.SubtotalAmount.Should().Be(20_000m);
        order.FinalAmount.Should().Be(18_000m);

        var balanceBefore = await WalletBalanceAsync(user.Id);
        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IPaymentService>().PayWithWalletAsync(user.Id, order.OrderId);

        await using (var verify = _fixture.CreateDbContext())
        {
            var stored = await verify.Orders.SingleAsync(x => x.Id == order.OrderId);
            stored.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
            stored.FinalAmount.Should().Be(18_000m);

            (await WalletBalanceAsync(user.Id)).Should().Be(balanceBefore - 18_000m, "the discounted amount is charged");
            // Exactly one SUCCESSFUL payment: other rows may exist for attempts, what must never
            // happen twice is a completed one.
            (await verify.Payments.CountAsync(x => x.OrderId == order.OrderId &&
                x.Status == (byte)PaymentStatus.Paid)).Should().Be(1);

            // Inventory follows quantity, not money: the coupon must not change what is consumed.
            var variant = await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id);
            variant.StockQuantity.Should().Be(3, "5 - 2 units");

            // The product information survives into the order snapshot.
            var item = await verify.OrderItems.Include(x => x.InputValues).SingleAsync(x => x.OrderId == order.OrderId);
            item.InputValues.Should().ContainSingle(x => x.FieldKey == "player_id" && x.Value == "WALLET-COUPON");
        }

        // A replayed wallet payment must not debit again or consume stock again.
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var again = () => scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(user.Id, order.OrderId);
            await again.Should().ThrowAsync<BusinessException>();
        }

        await using var final = _fixture.CreateDbContext();
        (await WalletBalanceAsync(user.Id)).Should().Be(balanceBefore - 18_000m, "exactly once");
        (await final.ProductVariants.SingleAsync(x => x.ProductId == product.Id)).StockQuantity.Should().Be(3);
        (await final.Payments.CountAsync(x => x.OrderId == order.OrderId &&
            x.Status == (byte)PaymentStatus.Paid)).Should().Be(1, "the replay added no second success");
    }

    [Fact]
    public async Task A_wallet_without_enough_balance_pays_nothing_and_leaves_stock_alone()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync(50_000m, ("player_id", true), stock: 4);
        await CreditWalletAsync(user.Id, 1_000m);

        var cart = await AddToCartAsync(user.Id, product.Id);
        await FillAsync(user.Id, cart.Items.Single().Id, 1, new() { ["player_id"] = "POOR" });
        var order = await CheckoutAsync(user.Id);

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var act = () => scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(user.Id, order.OrderId);
            await act.Should().ThrowAsync<BusinessException>();
        }

        await using var verify = _fixture.CreateDbContext();
        (await WalletBalanceAsync(user.Id)).Should().Be(1_000m);
        (await verify.Orders.SingleAsync(x => x.Id == order.OrderId)).PaymentStatus
            .Should().NotBe((byte)PaymentStatus.Paid);
        (await verify.ProductVariants.SingleAsync(x => x.ProductId == product.Id)).StockQuantity
            .Should().Be(4, "a failed payment consumes nothing");
    }

    // ---------------------------------------------------------------- helpers

    private CartService Cart(Vitorize.Infrastructure.Persistence.VitorizeDbContext db) =>
        new(db, _fixture.Factory.Services.GetRequiredService<IEncryptionService>(), new VatSettingsProvider(db));

    private async Task<CartDto> AddToCartAsync(Guid userId, Guid productId, int quantity = 1)
    {
        await using var db = _fixture.CreateDbContext();
        return await Cart(db).AddItemAsync(userId, new AddToCartRequestDto
        {
            ProductId = productId, Quantity = quantity
        });
    }

    private async Task FillAsync(Guid userId, Guid lineId, int quantity, Dictionary<string, string?> values)
    {
        await using var db = _fixture.CreateDbContext();
        await Cart(db).UpdateItemAsync(userId, lineId,
            new UpdateCartItemRequestDto { Quantity = quantity, InputValues = values });
    }

    private async Task<CheckoutResultDto> CheckoutAsync(Guid userId, string? couponCode = null)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
            .CheckoutAsync(userId, new CheckoutRequestDto { CouponCode = couponCode });
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

    private async Task<decimal> WalletBalanceAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        return (await db.Wallets.SingleAsync(x => x.UserId == userId)).Balance;
    }

    private async Task<int> WalletTransactionCountAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.WalletTransactions.CountAsync(x => x.Wallet.UserId == userId);
    }

    private async Task<Coupon> SeedPercentCouponAsync(decimal percent)
    {
        await using var db = _fixture.CreateDbContext();
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(), Code = $"CPN{Guid.NewGuid():N}"[..12].ToUpperInvariant(),
            DiscountType = 1, DiscountValue = percent, IsActive = true,
            Title = "Wallet coupon",
            StartsAt = DateTime.UtcNow.AddDays(-1), EndsAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }

    private async Task<Product> SeedProductAsync(decimal price, (string Key, bool Required) field, int stock = 10)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "gate", Slug = $"gate-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Gate product",
            Slug = $"gate-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = price, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.ProductInputFields.Add(new ProductInputField
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Key = field.Key, Label = field.Key,
            FieldType = (byte)ProductInputFieldType.Text, IsRequired = field.Required,
            DisplayStage = 1, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        product.WithCanonicalVariant(stock);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
