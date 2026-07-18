using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Phase 4 concurrency and race-condition coverage for surfaces not already exercised by
/// <see cref="FinancialConcurrencyIntegrationTests"/>: wallet debit / overdraw protection,
/// mixed credit+debit interleaving, per-user coupon limits, concurrent cart creation, and the
/// concurrent identical add-to-cart merge invariant. All scenarios run against real SQL Server
/// so the production application locks and isolation levels are the code under test.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Phase4ConcurrencyIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Phase4ConcurrencyIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Concurrent_wallet_debits_never_overdraw_and_leave_no_negative_balance()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        await using (var seed = _fixture.CreateDbContext())
            await new WalletService(seed, new NullNotifications()).CreditAsync(
                user.Id, 500m, (byte)WalletReferenceType.ManualAdminCharge, Guid.NewGuid(), "seed balance");

        // Ten concurrent debits of 100 against a 500 balance. Each carries a distinct reference so
        // idempotency cannot collapse them: exactly five must win, five must be rejected.
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(async _ =>
        {
            try
            {
                await using var db = _fixture.CreateDbContext();
                await new WalletService(db, new NullNotifications()).DebitAsync(
                    user.Id, 100m, (byte)WalletReferenceType.OrderPayment, Guid.NewGuid(), "concurrent debit");
                return true;
            }
            catch (BusinessException)
            {
                return false;
            }
        }));

        results.Count(succeeded => succeeded).Should().Be(5);

        await using var verify = _fixture.CreateDbContext();
        var balance = await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync();
        balance.Should().Be(0m);
        balance.Should().BeGreaterThanOrEqualTo(0m, "the wallet must never be overdrawn under concurrency");
        (await verify.WalletTransactions.CountAsync(x =>
            x.UserId == user.Id && x.Type == (byte)WalletTransactionType.Debit)).Should().Be(5);
    }

    [Fact]
    public async Task Concurrent_credit_and_debit_operations_keep_the_balance_consistent()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        await using (var seed = _fixture.CreateDbContext())
            await new WalletService(seed, new NullNotifications()).CreditAsync(
                user.Id, 1000m, (byte)WalletReferenceType.ManualAdminCharge, Guid.NewGuid(), "seed balance");

        // Six credits and six debits of 100 interleave. The serializable application lock keeps the
        // running balance non-negative throughout, so all twelve operations succeed and the net is zero.
        var operations = Enumerable.Range(0, 12).Select(index => index % 2 == 0).ToArray();
        await Task.WhenAll(operations.Select(async isCredit =>
        {
            await using var db = _fixture.CreateDbContext();
            var service = new WalletService(db, new NullNotifications());
            if (isCredit)
                await service.CreditAsync(user.Id, 100m, (byte)WalletReferenceType.Cashback, Guid.NewGuid(), "credit");
            else
                await service.DebitAsync(user.Id, 100m, (byte)WalletReferenceType.OrderPayment, Guid.NewGuid(), "debit");
        }));

        await using var verify = _fixture.CreateDbContext();
        // The balance is the real invariant: 1000 seed + 6*100 credited - 6*100 debited = 1000, with
        // no lost update. The transaction ledger holds 13 rows (the seed credit plus the 12 operations).
        (await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync()).Should().Be(1000m);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == user.Id)).Should().Be(13);
    }

    [Fact]
    public async Task Coupon_per_user_limit_has_exactly_one_winner_under_concurrency()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(), Code = $"PU{Guid.NewGuid():N}"[..18], Title = "Per-user concurrency coupon",
            DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = 10m,
            // Global limit is generous; the per-user limit of one is the guard being tested.
            MaxUsageCount = 100, MaxUsagePerUser = 1, UsedCount = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var orders = new[] { NewOrder(user.Id, coupon.Id), NewOrder(user.Id, coupon.Id) };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Coupons.Add(coupon);
            seed.Orders.AddRange(orders);
            await seed.SaveChangesAsync();
        }

        var results = await Task.WhenAll(orders.Select(async order =>
        {
            try
            {
                await using var db = _fixture.CreateDbContext();
                await new CouponService(db).MarkCouponAsUsedAsync(user.Id, order.Id, coupon.Id);
                return true;
            }
            catch
            {
                return false;
            }
        }));

        results.Count(succeeded => succeeded).Should().Be(1);
        await using var verify = _fixture.CreateDbContext();
        (await verify.CouponUsages.CountAsync(x => x.CouponId == coupon.Id && x.UserId == user.Id)).Should().Be(1);
        (await verify.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_cart_reads_create_exactly_one_cart()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var encryption = _fixture.Factory.Services.GetRequiredService<IEncryptionService>();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            await new CartService(db, encryption).GetAsync(user.Id);
        }));

        await using var verify = _fixture.CreateDbContext();
        (await verify.Carts.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_identical_add_to_cart_merges_into_a_single_line()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var encryption = _fixture.Factory.Services.GetRequiredService<IEncryptionService>();
        var product = await SeedSimpleProductAsync();

        // Six identical add-to-cart calls for the same product/variant/fingerprint must merge into a
        // single line with the summed quantity - never duplicate rows and never a lost quantity.
        await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            await new CartService(db, encryption).AddItemAsync(user.Id,
                new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        }));

        await using var verify = _fixture.CreateDbContext();
        var cart = await verify.Carts.Include(x => x.CartItems).SingleAsync(x => x.UserId == user.Id);
        cart.CartItems.Should().HaveCount(1, "identical items must merge instead of creating duplicate lines");
        cart.CartItems.Single().Quantity.Should().Be(6, "every concurrent add must be reflected in the merged quantity");
    }

    private async Task<Product> SeedSimpleProductAsync()
    {
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Cart race", Slug = $"cart-race-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Cart race product",
            Slug = $"cart-race-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 50m,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        await using var seed = _fixture.CreateDbContext();
        seed.Categories.Add(category);
        seed.Products.Add(product);
        await seed.SaveChangesAsync();
        return product;
    }

    private static Order NewOrder(Guid userId, Guid couponId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"VT-P4C-{Guid.NewGuid():N}",
        Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
        SubtotalAmount = 100m, DiscountAmount = 10m, FinalAmount = 90m,
        CouponId = couponId, CreatedAt = DateTime.UtcNow
    };

    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message) => Task.CompletedTask;
        public Task<List<NotificationDto>> GetMyNotificationsAsync(Guid userId) => Task.FromResult(new List<NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userId, Guid notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
    }
}
