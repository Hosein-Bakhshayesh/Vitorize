using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// SQL Server-backed coverage for multi-unit checkout of instant-delivery gift-card
/// products. These self-skip unless VITORIZE_SQL_TEST_CONNECTION points at a real
/// SQL Server (the reservation logic depends on UPDLOCK/ROWLOCK + serializable
/// isolation that EF InMemory cannot reproduce).
/// </summary>
public sealed class InstantMultiQuantityCheckoutTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("VITORIZE_SQL_TEST_CONNECTION");
    private static readonly IEncryptionService Crypto = new AesEncryptionService(
        Options.Create(new EncryptionSettings { Key = "0123456789abcdef0123456789abcdef" }));

    // ---------- happy-path quantities ----------

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(10)]
    public async Task Instant_checkout_reserves_exactly_N_distinct_codes(int quantity)
    {
        if (Connection is null) return;
        var (userId, product) = await SeedProductWithCodes(codeCount: quantity + 3);
        await SeedCart(userId, product.Id, quantity);

        CheckoutResultDto result;
        await using (var db = Db())
            result = await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());

        Assert.Equal(quantity, result.ReservationIds.Count);
        Assert.Equal(quantity, result.ReservationIds.Distinct().Count());

        await using var check = Db();
        var reservations = await check.GiftCodeReservations
            .Where(x => x.OrderId == result.OrderId).ToListAsync();
        Assert.Equal(quantity, reservations.Count);
        // exactly N distinct gift codes, each reserved once
        var codeIds = reservations.Select(x => x.GiftCodeId).ToList();
        Assert.Equal(quantity, codeIds.Distinct().Count());
        Assert.All(reservations, r => Assert.Equal((byte)GiftCodeReservationStatus.Active, r.Status));
        // order item quantity equals reservation count
        var item = await check.OrderItems.SingleAsync(x => x.OrderId == result.OrderId);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal(quantity, reservations.Count(r => r.OrderItemId == item.Id));
        // reserved codes are exactly the ones marked Reserved for this user
        var reservedCodes = await check.GiftCodes
            .Where(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Reserved).ToListAsync();
        Assert.Equal(quantity, reservedCodes.Count);
        Assert.All(reservedCodes, c => Assert.Equal(userId, c.ReservedByUserId));
    }

    // ---------- inventory boundary conditions ----------

    [Fact]
    public async Task Instant_checkout_with_exactly_enough_inventory_succeeds()
    {
        if (Connection is null) return;
        var (userId, product) = await SeedProductWithCodes(codeCount: 5);
        await SeedCart(userId, product.Id, 5);

        await using (var db = Db())
        {
            var result = await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());
            Assert.Equal(5, result.ReservationIds.Count);
        }
        await using var check = Db();
        Assert.Equal(0, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available));
    }

    [Theory]
    [InlineData(3, 2)]   // one less than required
    [InlineData(5, 4)]   // one less than required
    [InlineData(10, 1)]  // far short
    public async Task Instant_checkout_with_insufficient_inventory_fails_cleanly(int quantity, int available)
    {
        if (Connection is null) return;
        var (userId, product) = await SeedProductWithCodes(codeCount: available);
        await SeedCart(userId, product.Id, quantity);

        await using (var db = Db())
        {
            var ex = await Assert.ThrowsAsync<BusinessException>(
                () => NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto()));
            Assert.Contains("موجودی", ex.Message);
        }

        // full rollback: no order, no reservations, every code still Available, cart intact
        await using var check = Db();
        Assert.Equal(0, await check.Orders.CountAsync(x => x.UserId == userId));
        Assert.Equal(0, await check.GiftCodeReservations.CountAsync(x => x.UserId == userId));
        Assert.Equal(available, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available));
        Assert.Equal(0, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Reserved));
        Assert.True(await check.CartItems.AnyAsync(x => x.Cart.UserId == userId));
    }

    // ---------- end-to-end fulfilment: pay, deliver, library, admin ----------

    [Fact]
    public async Task Instant_multi_quantity_full_flow_delivers_distinct_codes()
    {
        if (Connection is null) return;
        const int quantity = 3;
        var (userId, product) = await SeedProductWithCodes(codeCount: quantity);
        await SeedCart(userId, product.Id, quantity);

        Guid orderId;
        Guid paymentId;
        await using (var db = Db())
        {
            var result = await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());
            orderId = result.OrderId;
            paymentId = await db.Payments.Where(x => x.OrderId == orderId).Select(x => x.Id).SingleAsync();
        }

        // pay (mock) -> triggers real gift-code delivery
        await using (var db = Db())
            await NewPayment(db).VerifyMockPaymentAsync(userId, paymentId);

        await using var check = Db();
        var order = await check.Orders.SingleAsync(x => x.Id == orderId);
        Assert.Equal((byte)PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal((byte)OrderStatus.Completed, order.Status);

        var deliveries = await check.OrderItemDeliveries
            .Where(d => d.OrderItem.OrderId == orderId).ToListAsync();
        Assert.Equal(quantity, deliveries.Count);
        Assert.Equal(quantity, deliveries.Select(d => d.GiftCodeId).Distinct().Count()); // distinct codes
        Assert.Equal(quantity, deliveries.Select(d => d.ContentHash).Distinct().Count()); // distinct content

        // order item quantity == delivered count
        var item = await check.OrderItems.SingleAsync(x => x.OrderId == orderId);
        Assert.Equal(quantity, item.Quantity);
        Assert.Equal((byte)DeliveryStatus.Delivered, item.DeliveryStatus);

        // all gift codes for the product are Delivered
        Assert.Equal(quantity, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Delivered));

        // customer library returns exactly N codes
        var library = await NewOrderService(check).GetMyDeliveredCodesAsync(userId);
        var forOrder = library.Where(x => x.OrderId == orderId).ToList();
        Assert.Equal(quantity, forOrder.Count);

        // admin order detail shows the correct delivered quantity
        var adminDetail = await NewOrderService(check).GetAdminOrderDetailsAsync(orderId);
        var adminItem = Assert.Single(adminDetail.Items);
        Assert.Equal(quantity, adminItem.Quantity);
        Assert.Equal(quantity, adminItem.Deliveries.Count);
    }

    [Fact]
    public async Task Duplicate_mock_verification_does_not_duplicate_delivery()
    {
        if (Connection is null) return;
        const int quantity = 2;
        var (userId, product) = await SeedProductWithCodes(codeCount: quantity);
        await SeedCart(userId, product.Id, quantity);

        Guid orderId, paymentId;
        await using (var db = Db())
        {
            var result = await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());
            orderId = result.OrderId;
            paymentId = await db.Payments.Where(x => x.OrderId == orderId).Select(x => x.Id).SingleAsync();
        }

        // verify twice (duplicate callback / repeated verification)
        await using (var db = Db()) await NewPayment(db).VerifyMockPaymentAsync(userId, paymentId);
        await using (var db = Db()) await NewPayment(db).VerifyMockPaymentAsync(userId, paymentId);

        await using var check = Db();
        Assert.Equal(quantity, await check.OrderItemDeliveries.CountAsync(d => d.OrderItem.OrderId == orderId));
        Assert.Equal(quantity, await check.GiftCodeReservations.CountAsync(r => r.OrderId == orderId));
    }

    [Fact]
    public async Task Duplicate_checkout_submission_is_idempotent_on_cart()
    {
        if (Connection is null) return;
        var (userId, product) = await SeedProductWithCodes(codeCount: 5);
        await SeedCart(userId, product.Id, 2);

        await using (var db = Db())
            await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());

        // second submit: cart was emptied by the first -> controlled business error, no second order
        await using (var db = Db())
            await Assert.ThrowsAsync<BusinessException>(() => NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto()));

        await using var check = Db();
        Assert.Equal(1, await check.Orders.CountAsync(x => x.UserId == userId));
        Assert.Equal(2, await check.GiftCodeReservations.CountAsync(x => x.UserId == userId));
    }

    // ---------- concurrency ----------

    [Fact]
    public async Task Two_customers_with_enough_inventory_both_succeed_with_disjoint_codes()
    {
        if (Connection is null) return;
        var (u1, product) = await SeedProductWithCodes(codeCount: 4);
        var u2 = await SeedUser();
        await SeedCart(u1, product.Id, 2);
        await SeedCart(u2, product.Id, 2);

        var results = await Task.WhenAll(
            RunCheckout(u1), RunCheckout(u2));

        Assert.All(results, r => Assert.NotNull(r));
        Assert.All(results, r => Assert.Equal(2, r!.ReservationIds.Count));

        await using var check = Db();
        var reservations = await check.GiftCodeReservations
            .Where(x => x.ProductId == product.Id).ToListAsync();
        Assert.Equal(4, reservations.Count);
        Assert.Equal(4, reservations.Select(x => x.GiftCodeId).Distinct().Count()); // no code shared
        Assert.Equal(0, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available));
    }

    [Fact]
    public async Task Two_customers_with_insufficient_final_inventory_one_wins_no_shared_code()
    {
        if (Connection is null) return;
        var (u1, product) = await SeedProductWithCodes(codeCount: 3); // only 3 for two x2 requests
        var u2 = await SeedUser();
        await SeedCart(u1, product.Id, 2);
        await SeedCart(u2, product.Id, 2);

        var results = await Task.WhenAll(RunCheckoutSafe(u1), RunCheckoutSafe(u2));
        var winners = results.Count(x => x is not null);
        Assert.Equal(1, winners); // exactly one full success; the other fails cleanly

        await using var check = Db();
        var reservations = await check.GiftCodeReservations.Where(x => x.ProductId == product.Id).ToListAsync();
        Assert.Equal(2, reservations.Count); // only the winner's two codes
        Assert.Equal(2, reservations.Select(x => x.GiftCodeId).Distinct().Count());
        // one order only; loser left no partial rows
        Assert.Equal(1, await check.Orders.CountAsync(x => x.UserId == u1 || x.UserId == u2));
        Assert.Equal(1, await check.GiftCodes.CountAsync(x => x.ProductId == product.Id && x.Status == (byte)GiftCodeStatus.Available));
    }

    // ---------- helpers ----------

    private Task<CheckoutResultDto?> RunCheckoutSafe(Guid userId) => Run(userId, safe: true);
    private Task<CheckoutResultDto?> RunCheckout(Guid userId) => Run(userId, safe: false);
    private async Task<CheckoutResultDto?> Run(Guid userId, bool safe)
    {
        try
        {
            await using var db = Db();
            return await NewCheckout(db).CheckoutAsync(userId, new CheckoutRequestDto());
        }
        catch (BusinessException) when (safe) { return null; }
    }

    private static CheckoutService NewCheckout(VitorizeDbContext db) =>
        new(db, new NullCoupon(), new NullNotifications(), Crypto);

    private static PaymentService NewPayment(VitorizeDbContext db) =>
        new(db, new GiftCodeDeliveryService(db, Crypto), new NullCoupon(), new NullWallet(),
            new NullNotifications(), new UnusedGateway(), new NullSmsOutbox());

    private static OrderService NewOrderService(VitorizeDbContext db) =>
        new(db, new NullNotifications(), Crypto);

    private static async Task<(Guid userId, Product product)> SeedProductWithCodes(int codeCount)
    {
        var userId = (await SeedUserEntity()).Id;
        var category = new Category { Id = Guid.NewGuid(), Title = "mq", Slug = $"mq-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "گیفت کارت تست", Slug = $"mq-p-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100, CurrencyType = 1,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var codes = Enumerable.Range(0, codeCount).Select(i => new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = Crypto.Encrypt($"CODE-{Guid.NewGuid():N}"),
            Status = (byte)GiftCodeStatus.Available, EncryptionVersion = 2, CodeHashFingerprint = Guid.NewGuid().ToString("N"),
            CreatedAt = DateTime.UtcNow.AddSeconds(i)
        }).ToList();
        await using var db = Db();
        db.Categories.Add(category); db.Products.Add(product); db.GiftCodes.AddRange(codes);
        // attach user already saved
        await db.SaveChangesAsync();
        return (userId, product);
    }

    private static async Task<Guid> SeedUser() => (await SeedUserEntity()).Id;
    private static async Task<User> SeedUserEntity()
    {
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "MQ test", Mobile = "09" + Random.Shared.NextInt64(100000000, 999999999),
            PasswordHash = "not-a-real-credential", Status = (byte)UserStatus.Active,
            VerificationStatus = (byte)VerificationStatus.Verified, IsMobileConfirmed = true, CreatedAt = DateTime.UtcNow
        };
        await using var db = Db();
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task SeedCart(Guid userId, Guid productId, int quantity)
    {
        var cart = new Cart { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow };
        var item = new CartItem
        {
            Id = Guid.NewGuid(), CartId = cart.Id, ProductId = productId, Quantity = quantity,
            UnitPrice = 100, CurrencyType = 1, InputFingerprint = "", CreatedAt = DateTime.UtcNow
        };
        await using var db = Db();
        db.Carts.Add(cart); db.CartItems.Add(item);
        await db.SaveChangesAsync();
    }

    private static VitorizeDbContext Db() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseSqlServer(Connection!).Options);

    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid u, byte t, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid u, string t, string m) => Task.CompletedTask;
        public Task<List<NotificationDto>> GetMyNotificationsAsync(Guid u) => Task.FromResult(new List<NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid u) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid u, Guid n) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid u) => Task.CompletedTask;
    }
    private sealed class NullCoupon : ICouponService
    {
        public Task<Vitorize.Application.DTOs.Coupons.ValidateCouponResultDto> ValidateAsync(Guid u, Vitorize.Application.DTOs.Coupons.ValidateCouponRequestDto r) => throw new NotSupportedException();
        public Task MarkCouponAsUsedAsync(Guid u, Guid o, Guid c) => Task.CompletedTask;
    }
    private sealed class NullWallet : IWalletService
    {
        public Task<WalletDto> CreditAsync(Guid u, decimal a, byte? rt, Guid? ri, string? d) => throw new NotSupportedException();
        public Task<WalletDto> DebitAsync(Guid u, decimal a, byte? rt, Guid? ri, string? d) => throw new NotSupportedException();
        public Task<WalletDto> GetMyWalletAsync(Guid u) => throw new NotSupportedException();
        public Task<List<WalletTransactionDto>> GetMyTransactionsAsync(Guid u) => throw new NotSupportedException();
        public Task<WalletDto> GetUserWalletAsync(Guid u) => throw new NotSupportedException();
        public Task<List<WalletTransactionDto>> GetUserTransactionsAsync(Guid u) => throw new NotSupportedException();
        public Task<WalletDto> AdminChargeAsync(WalletChargeRequestDto r) => throw new NotSupportedException();
        public Task<WalletDto> AdminWithdrawAsync(WalletWithdrawRequestDto r) => throw new NotSupportedException();
    }
    private sealed class UnusedGateway : IZarinpalGatewayService
    {
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal a, CurrencyType currency, string d, string? m = null, string? e = null, string? o = null) => throw new NotSupportedException();
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string a, decimal amount) => throw new NotSupportedException();
        public Task<string> BuildPaymentUrlAsync(string authority) => throw new NotSupportedException();
    }
    private sealed class NullSmsOutbox : ISmsOutboxEnqueuer
    {
        public Task EnqueueTemplateAsync(string? m, string k, IReadOnlyList<SmsTemplateParameter> p, string purpose, Guid? a, CancellationToken c = default, Guid? u = null, Guid? by = null, string? et = null, string? er = null, string? ik = null, string? n = null) => Task.CompletedTask;
        public Task EnqueueTextAsync(string? m, string t, string purpose, Guid? a, CancellationToken c = default, Guid? u = null, Guid? by = null, string? et = null, string? er = null, string? ik = null, string? n = null) => Task.CompletedTask;
    }
}
