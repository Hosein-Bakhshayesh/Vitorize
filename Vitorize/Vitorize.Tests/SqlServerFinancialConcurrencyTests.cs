using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.GiftCodes;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.Models.Sms;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;
using Microsoft.Extensions.Options;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Common;

namespace Vitorize.Tests;

public sealed class SqlServerFinancialConcurrencyTests
{
    private static string? Connection => Environment.GetEnvironmentVariable("VITORIZE_SQL_TEST_CONNECTION");

    [Fact]
    public async Task Phase3_redirect_tags_and_sitemap_use_the_real_sql_schema()
    {
        if (Connection is null) return;
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "seo-sql", Slug = $"seo-sql-{Guid.NewGuid():N}",
            FocusKeyword = "خرید امن", ImageAltText = "دسته تست سئو", IsActive = true,
            CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "seo-sql",
            Slug = $"seo-product-{Guid.NewGuid():N}", FocusKeyword = "محصول تست",
            ThumbnailAltText = "تصویر محصول تست", ProductType = 1,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = 100,
            CurrencyType = 1, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var tag = new ProductTag
        {
            Id = Guid.NewGuid(), Title = $"tag-{Guid.NewGuid():N}", Slug = $"tag-{Guid.NewGuid():N}",
            Aliases = "alias,نام مستعار", IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.Tags.Add(tag);
        var source = $"/legacy-{Guid.NewGuid():N}";
        await using (var seed = Db())
        {
            seed.Categories.Add(category);
            seed.Products.Add(product);
            seed.LegacyRedirects.Add(new LegacyRedirect
            {
                Id = Guid.NewGuid(), SourcePath = source, DestinationPath = $"/product/{product.Slug}",
                StatusCode = 301, IsActive = true, CreatedAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        await using var db = Db();
        var seo = new SeoService(db);
        var redirect = await seo.ResolveRedirectAsync(source + "/?ignored=1");
        var sitemap = await seo.GetSitemapAsync("products", 1, 50_000);

        Assert.Equal($"/product/{product.Slug}", redirect?.DestinationPath);
        Assert.Contains(sitemap.Items, x => x.Path == $"/product/{product.Slug}");
        Assert.Contains(await db.ProductTags.AsNoTracking().ToListAsync(),
            x => x.Id == tag.Id && x.Aliases == "alias,نام مستعار" && x.IsActive);
    }

    [Fact]
    public async Task Wallet_concurrent_credits_are_not_lost_and_replay_is_idempotent()
    {
        if (Connection is null) return;
        var user = NewUser();
        await using (var seed = Db()) { seed.Users.Add(user); await seed.SaveChangesAsync(); }
        var references = Enumerable.Range(0, 8).Select(_ => Guid.NewGuid()).ToArray();
        await Task.WhenAll(references.Select(async reference =>
        {
            await using var db = Db();
            await new WalletService(db, new NullNotifications()).CreditAsync(
                user.Id, 100, (byte)WalletReferenceType.Cashback, reference, "concurrency-test");
        }));
        await using (var db = Db())
        {
            await new WalletService(db, new NullNotifications()).CreditAsync(
                user.Id, 100, (byte)WalletReferenceType.Cashback, references[0], "replay");
            Assert.Equal(800, await db.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync());
            Assert.Equal(8, await db.WalletTransactions.CountAsync(x => x.UserId == user.Id));
        }
    }

    [Fact]
    public async Task Checkout_reprices_authoritative_variant_and_validates_catalog_state()
    {
        if (Connection is null) return;
        var user = NewUser();
        var category = new Category { Id = Guid.NewGuid(), Title = "checkout", Slug = $"checkout-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "checkout", Slug = $"checkout-p-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 100,
            CurrencyType = 1, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "active", Price = 175,
            StockMode = (byte)ProductVariantStockMode.Manual, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var cart = new Cart { Id = Guid.NewGuid(), UserId = user.Id, CreatedAt = DateTime.UtcNow };
        var item = new CartItem
        {
            Id = Guid.NewGuid(), CartId = cart.Id, ProductId = product.Id, ProductVariantId = variant.Id,
            Quantity = 2, UnitPrice = 1, InputFingerprint = "", CreatedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.Add(user); seed.Categories.Add(category); seed.Products.Add(product);
            seed.ProductVariants.Add(variant); seed.Carts.Add(cart); seed.CartItems.Add(item); await seed.SaveChangesAsync();
        }
        await using var db = Db();
        var service = new CheckoutService(db, new NullCoupon(), new NullNotifications(),
            new AesEncryptionService(Options.Create(new EncryptionSettings { Key = "0123456789abcdef0123456789abcdef" })));
        var result = await service.CheckoutAsync(user.Id, new Vitorize.Application.DTOs.Checkout.CheckoutRequestDto());
        Assert.Equal(350, result.SubtotalAmount);
        var persisted = await db.OrderItems.SingleAsync(x => x.OrderId == result.OrderId);
        Assert.Equal(175, persisted.UnitPrice);
    }

    [Fact]
    public async Task Coupon_global_limit_survives_concurrent_orders()
    {
        if (Connection is null) return;
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(), Code = $"C{Guid.NewGuid():N}"[..20], Title = "race",
            DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = 10,
            MaxUsageCount = 1, UsedCount = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var users = new[] { NewUser(), NewUser() };
        var orders = users.Select((u, i) => NewOrder(u.Id, coupon.Id, $"CP-{Guid.NewGuid():N}")).ToArray();
        await using (var seed = Db())
        {
            seed.Users.AddRange(users); seed.Coupons.Add(coupon); seed.Orders.AddRange(orders);
            await seed.SaveChangesAsync();
        }
        var attempts = await Task.WhenAll(orders.Select(async (order, i) =>
        {
            try
            {
                await using var db = Db();
                await new CouponService(db).MarkCouponAsUsedAsync(users[i].Id, order.Id, coupon.Id);
                return true;
            }
            catch { return false; }
        }));
        Assert.Equal(1, attempts.Count(x => x));
        await using var check = Db();
        Assert.Equal(1, await check.CouponUsages.CountAsync(x => x.CouponId == coupon.Id));
        Assert.Equal(1, await check.Coupons.Where(x => x.Id == coupon.Id).Select(x => x.UsedCount).SingleAsync());
    }

    [Fact]
    public async Task Gift_code_reservation_race_has_one_winner()
    {
        if (Connection is null) return;
        var users = new[] { NewUser(), NewUser() };
        var category = new Category { Id = Guid.NewGuid(), Title = "race", Slug = $"race-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "code", Slug = $"code-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 10,
            CurrencyType = 1, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var code = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = "test", Status = (byte)GiftCodeStatus.Available,
            EncryptionVersion = 2, CodeHashFingerprint = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.AddRange(users); seed.Categories.Add(category); seed.Products.Add(product); seed.GiftCodes.Add(code);
            await seed.SaveChangesAsync();
        }
        var errors = new System.Collections.Concurrent.ConcurrentBag<string>();
        var wins = await Task.WhenAll(users.Select(async user =>
        {
            try
            {
                await using var db = Db();
                await new GiftCodeReservationService(db).ReserveAsync(user.Id,
                    new ReserveGiftCodeRequestDto { ProductId = product.Id });
                return true;
            }
            catch (Exception ex) { errors.Add(ex.ToString()); return false; }
        }));
        Assert.True(wins.Count(x => x) == 1, string.Join(Environment.NewLine, errors));
        await using var check = Db();
        Assert.Equal(1, await check.GiftCodeReservations.CountAsync(x => x.GiftCodeId == code.Id));
    }

    [Fact]
    public async Task Duplicate_gateway_callbacks_complete_once()
    {
        if (Connection is null) return;
        var user = NewUser();
        var order = NewOrder(user.Id, null, $"CB-{Guid.NewGuid():N}");
        order.DiscountAmount = 0; order.FinalAmount = 100; order.SubtotalAmount = 100;
        var authority = $"A-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, UserId = user.Id, Amount = 100,
            Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.Add(user); seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }
        var gateways = new[] { new SuccessfulGateway(), new SuccessfulGateway() };
        await Task.WhenAll(gateways.Select(async gateway =>
        {
            await using var db = Db();
            var service = new PaymentService(db, new NullGiftDelivery(), new NullCoupon(),
                new NullWallet(), new NullNotifications(), gateway, new NullSmsOutbox());
            await service.VerifyZarinpalPaymentAsync(authority, "OK");
        }));
        await using var check = Db();
        Assert.Equal((byte)PaymentStatus.Paid,
            await check.Payments.Where(x => x.Id == payment.Id).Select(x => x.Status).SingleAsync());
        Assert.Single(await check.PaymentCallbacks.Where(x => x.PaymentId == payment.Id).ToListAsync());
        Assert.Equal(1, gateways.Sum(x => x.VerifyCount));
    }

    [Fact]
    public async Task Verified_payment_with_failed_fulfillment_is_compensated_to_wallet()
    {
        if (Connection is null) return;
        var user = NewUser();
        var category = new Category { Id = Guid.NewGuid(), Title = "comp", Slug = $"comp-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "instant", Slug = $"instant-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100,
            CurrencyType = 1, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var order = NewOrder(user.Id, null, $"CO-{Guid.NewGuid():N}");
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 100, TotalPrice = 100, DeliveryType = (byte)DeliveryType.Instant,
            DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        var authority = $"CO-A-{Guid.NewGuid():N}";
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, UserId = user.Id, Amount = 100,
            Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending, RequestedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.Add(user); seed.Categories.Add(category); seed.Products.Add(product);
            seed.Orders.Add(order); seed.OrderItems.Add(item); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }
        await using (var db = Db())
        {
            var wallet = new WalletService(db, new NullNotifications());
            var service = new PaymentService(db, new NullGiftDelivery(), new NullCoupon(), wallet,
                new NullNotifications(), new SuccessfulGateway(), new NullSmsOutbox());
            var result = await service.VerifyZarinpalPaymentAsync(authority, "OK");
            Assert.False(result.IsPaid);
            Assert.Equal((byte)PaymentStatus.Refunded, result.PaymentStatus);
        }
        await using var check = Db();
        Assert.Equal(100, await check.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync());
        Assert.Single(await check.PaymentRefunds.Where(x => x.PaymentId == payment.Id).ToListAsync());
    }

    [Fact]
    public async Task Wallet_refund_is_atomic_idempotent_and_audited()
    {
        if (Connection is null) return;
        var user = NewUser(); var admin = NewUser();
        var order = NewOrder(user.Id, null, $"RF-{Guid.NewGuid():N}");
        order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var payment = new Payment
        {
            Id = Guid.NewGuid(), OrderId = order.Id, UserId = user.Id, Amount = order.FinalAmount,
            Gateway = "Zarinpal", Authority = $"RF-A-{Guid.NewGuid():N}", Status = (byte)PaymentStatus.Paid,
            RequestedAt = DateTime.UtcNow, VerifiedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.AddRange(user, admin); seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }
        var request = new PaymentRefundRequestDto
        {
            Method = (byte)PaymentRefundMethod.Wallet, Reason = "integration test",
            IdempotencyKey = $"refund-{Guid.NewGuid():N}"
        };
        await using (var db = Db())
        {
            var wallet = new WalletService(db, new NullNotifications());
            var service = new PaymentService(db, new NullGiftDelivery(), new NullCoupon(), wallet,
                new NullNotifications(), new SuccessfulGateway(), new NullSmsOutbox());
            var first = await service.RefundAsync(payment.Id, admin.Id, request);
            var replay = await service.RefundAsync(payment.Id, admin.Id, request);
            Assert.Equal(first.Id, replay.Id);
        }
        await using var check = Db();
        Assert.Equal(1, await check.PaymentRefunds.CountAsync(x => x.PaymentId == payment.Id));
        Assert.Equal(order.FinalAmount, await check.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync());
        Assert.Equal((byte)PaymentStatus.Refunded, await check.Payments.Where(x => x.Id == payment.Id).Select(x => x.Status).SingleAsync());
        Assert.Contains(await check.FinancialAuditLogs.Where(x => x.CorrelationId == order.Id).ToListAsync(),
            x => x.EventType == "PaymentRefundCompleted");
    }

    [Fact]
    public async Task Manual_delivery_is_encrypted_single_use_and_audited()
    {
        if (Connection is null) return;
        var user = NewUser(); var admin = NewUser();
        var category = new Category { Id = Guid.NewGuid(), Title = "manual", Slug = $"manual-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "manual", Slug = $"manual-p-{Guid.NewGuid():N}",
            ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 100, CurrencyType = 1,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var order = NewOrder(user.Id, null, $"MD-{Guid.NewGuid():N}");
        order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
            Quantity = 1, UnitPrice = 100, TotalPrice = 100, DeliveryType = (byte)DeliveryType.Manual,
            DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.AddRange(user, admin); seed.Categories.Add(category); seed.Products.Add(product);
            seed.Orders.Add(order); seed.OrderItems.Add(item); await seed.SaveChangesAsync();
        }
        var crypto = new AesEncryptionService(Options.Create(new EncryptionSettings
        { Key = "0123456789abcdef0123456789abcdef" }));
        await using (var db = Db())
        {
            var service = new OrderService(db, new NullNotifications(), crypto);
            await service.DeliverManualAsync(order.Id, admin.Id,
                new ManualDeliveryRequestDto { OrderItemId = item.Id, Content = "private delivery" });
            await Assert.ThrowsAnyAsync<Exception>(() => service.DeliverManualAsync(order.Id, admin.Id,
                new ManualDeliveryRequestDto { OrderItemId = item.Id, Content = "duplicate" }));
        }
        await using var check = Db();
        var delivery = await check.OrderItemDeliveries.SingleAsync(x => x.OrderItemId == item.Id);
        Assert.Equal((short)2, delivery.EncryptionVersion);
        Assert.NotEqual("private delivery", delivery.DeliveredContent);
        Assert.Equal("private delivery", crypto.Decrypt(delivery.DeliveredContent!));
        Assert.Contains(await check.FinancialAuditLogs.Where(x => x.CorrelationId == order.Id).ToListAsync(),
            x => x.EventType == "ManualDeliveryCompleted");
    }

    [Fact]
    public async Task Kyc_document_token_is_owner_bound_and_storage_token_is_not_exposed()
    {
        if (Connection is null) return;
        var user = NewUser(); var other = NewUser();
        var profile = new UserVerificationProfile
        {
            Id = Guid.NewGuid(), UserId = user.Id, FirstName = "legacy", LastName = "legacy",
            NationalCode = "legacy", Status = (byte)VerificationStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        await using (var seed = Db())
        {
            seed.Users.AddRange(user, other); seed.UserVerificationProfiles.Add(profile); await seed.SaveChangesAsync();
        }
        await using var db = Db();
        var service = new VerificationService(db, new NullNotifications(), new NullSmsOutbox(),
            new AesEncryptionService(Options.Create(new EncryptionSettings { Key = "0123456789abcdef0123456789abcdef" })));
        await Assert.ThrowsAnyAsync<Exception>(() => service.AddDocumentAsync(user.Id, 1,
            $"kyc-private:{other.Id:N}/{Guid.NewGuid():N}.png"));
        var dto = await service.AddDocumentAsync(user.Id, 1,
            $"kyc-private:{user.Id:N}/{Guid.NewGuid():N}.png");
        Assert.Equal($"/api/verification/documents/{dto.Id}/content", dto.FilePath);
        Assert.DoesNotContain("kyc-private", dto.FilePath);
    }

    private static VitorizeDbContext Db() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseSqlServer(Connection!).Options);

    private static User NewUser() => new()
    {
        Id = Guid.NewGuid(), FullName = "SQL test", Mobile = "09" + Random.Shared.NextInt64(100000000, 999999999),
        PasswordHash = "not-a-real-credential", Status = (byte)UserStatus.Active,
        VerificationStatus = (byte)VerificationStatus.Verified, IsMobileConfirmed = true, CreatedAt = DateTime.UtcNow
    };

    private static Order NewOrder(Guid userId, Guid? couponId, string number) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = number, Status = (byte)OrderStatus.PendingPayment,
        PaymentStatus = (byte)PaymentStatus.Pending, SubtotalAmount = 100, DiscountAmount = couponId.HasValue ? 10 : 0,
        FinalAmount = couponId.HasValue ? 90 : 100, CouponId = couponId, CreatedAt = DateTime.UtcNow
    };

    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid u, byte t, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid u, string t, string m) => Task.CompletedTask;
        public Task<List<NotificationDto>> GetMyNotificationsAsync(Guid u) => Task.FromResult(new List<NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid u) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid u, Guid n) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid u) => Task.CompletedTask;
    }
    private sealed class SuccessfulGateway : IZarinpalGatewayService
    {
        public int VerifyCount { get; private set; }
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal a, string d, string? m = null, string? e = null, string? o = null) => throw new NotSupportedException();
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string a, decimal amount) { VerifyCount++; return Task.FromResult((true, 12345L)); }
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult("https://example.invalid");
    }
    private sealed class NullGiftDelivery : IGiftCodeDeliveryService { public Task DeliverOrderAsync(Guid o, Guid? u = null) => Task.CompletedTask; }
    private sealed class NullCoupon : ICouponService
    {
        public Task<ValidateCouponResultDto> ValidateAsync(Guid u, ValidateCouponRequestDto r) => throw new NotSupportedException();
        public Task MarkCouponAsUsedAsync(Guid u, Guid o, Guid c) => Task.CompletedTask;
    }
    private sealed class NullWallet : IWalletService
    {
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> CreditAsync(Guid u, decimal a, byte? rt, Guid? ri, string? d) => throw new NotSupportedException();
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> DebitAsync(Guid u, decimal a, byte? rt, Guid? ri, string? d) => throw new NotSupportedException();
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> GetMyWalletAsync(Guid u) => throw new NotSupportedException();
        public Task<List<Vitorize.Application.DTOs.Wallet.WalletTransactionDto>> GetMyTransactionsAsync(Guid u) => throw new NotSupportedException();
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> GetUserWalletAsync(Guid u) => throw new NotSupportedException();
        public Task<List<Vitorize.Application.DTOs.Wallet.WalletTransactionDto>> GetUserTransactionsAsync(Guid u) => throw new NotSupportedException();
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> AdminChargeAsync(Vitorize.Application.DTOs.Wallet.WalletChargeRequestDto r) => throw new NotSupportedException();
        public Task<Vitorize.Application.DTOs.Wallet.WalletDto> AdminWithdrawAsync(Vitorize.Application.DTOs.Wallet.WalletWithdrawRequestDto r) => throw new NotSupportedException();
    }
    private sealed class NullSmsOutbox : ISmsOutboxEnqueuer
    {
        public Task EnqueueTemplateAsync(string? m, string k, IReadOnlyList<SmsTemplateParameter> p, string purpose, Guid? a, CancellationToken c = default, Guid? u = null, Guid? by = null, string? et = null, string? er = null, string? ik = null, string? n = null) => Task.CompletedTask;
        public Task EnqueueTextAsync(string? m, string t, string purpose, Guid? a, CancellationToken c = default, Guid? u = null, Guid? by = null, string? et = null, string? er = null, string? ik = null, string? n = null) => Task.CompletedTask;
    }
}
