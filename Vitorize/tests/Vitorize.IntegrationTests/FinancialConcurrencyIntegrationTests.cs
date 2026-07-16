using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.GiftCodes;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class FinancialConcurrencyIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public FinancialConcurrencyIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Concurrent_wallet_credits_are_not_lost_and_duplicate_reference_is_idempotent()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var references = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();

        await Task.WhenAll(references.Select(async reference =>
        {
            await using var db = _fixture.CreateDbContext();
            await new WalletService(db, new NullNotifications()).CreditAsync(
                user.Id, 100m, (byte)WalletReferenceType.Cashback, reference, "integration concurrency");
        }));

        await using (var replayDb = _fixture.CreateDbContext())
            await new WalletService(replayDb, new NullNotifications()).CreditAsync(
                user.Id, 100m, (byte)WalletReferenceType.Cashback, references[0], "replay");

        await using var verify = _fixture.CreateDbContext();
        (await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync()).Should().Be(800m);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == user.Id)).Should().Be(8);
    }

    [Fact]
    public async Task Coupon_global_usage_limit_has_exactly_one_winner_under_concurrency()
    {
        var (first, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (second, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(), Code = $"C{Guid.NewGuid():N}"[..20], Title = "Concurrency coupon",
            DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = 10m,
            MaxUsageCount = 1, UsedCount = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var orders = new[] { NewOrder(first.Id, coupon.Id), NewOrder(second.Id, coupon.Id) };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Coupons.Add(coupon);
            seed.Orders.AddRange(orders);
            await seed.SaveChangesAsync();
        }

        var users = new[] { first, second };
        var results = await Task.WhenAll(orders.Select(async (order, index) =>
        {
            try
            {
                await using var db = _fixture.CreateDbContext();
                await new CouponService(db).MarkCouponAsUsedAsync(users[index].Id, order.Id, coupon.Id);
                return true;
            }
            catch { return false; }
        }));

        results.Count(x => x).Should().Be(1);
        await using var verify = _fixture.CreateDbContext();
        (await verify.CouponUsages.CountAsync(x => x.CouponId == coupon.Id)).Should().Be(1);
        (await verify.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Single_gift_code_reservation_has_exactly_one_winner_under_concurrency()
    {
        var (first, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (second, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "Gift race", Slug = $"gift-race-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "One code",
            Slug = $"one-code-{Guid.NewGuid():N}", ProductType = (byte)ProductType.GiftCard,
            DeliveryType = (byte)DeliveryType.Instant, BasePrice = 10m,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var giftCode = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = "encrypted-integration-value",
            CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2,
            Status = (byte)GiftCodeStatus.Available, CreatedAt = DateTime.UtcNow
        };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product); seed.GiftCodes.Add(giftCode);
            await seed.SaveChangesAsync();
        }

        var users = new[] { first, second };
        var results = await Task.WhenAll(users.Select(async user =>
        {
            try
            {
                await using var db = _fixture.CreateDbContext();
                await new GiftCodeReservationService(db).ReserveAsync(user.Id,
                    new ReserveGiftCodeRequestDto { ProductId = product.Id });
                return true;
            }
            catch { return false; }
        }));

        results.Count(x => x).Should().Be(1);
        await using var verify = _fixture.CreateDbContext();
        (await verify.GiftCodeReservations.CountAsync(x => x.GiftCodeId == giftCode.Id)).Should().Be(1);
    }

    private static Order NewOrder(Guid userId, Guid couponId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"VT-COUPON-{Guid.NewGuid():N}",
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
