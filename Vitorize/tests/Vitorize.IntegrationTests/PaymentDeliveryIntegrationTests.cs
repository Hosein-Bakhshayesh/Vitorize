using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class PaymentDeliveryIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public PaymentDeliveryIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Duplicate_gateway_callbacks_verify_and_complete_payment_exactly_once()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var authority = $"AUTH-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }
        var gateways = new[] { new SuccessfulGateway(), new SuccessfulGateway() };
        await Task.WhenAll(gateways.Select(async gateway =>
        {
            await using var db = _fixture.CreateDbContext();
            var service = NewPaymentService(db, gateway, new NullWallet());
            await service.VerifyZarinpalPaymentAsync(authority, "OK");
        }));

        await using var verify = _fixture.CreateDbContext();
        (await verify.Payments.SingleAsync(x => x.Id == payment.Id)).Status.Should().Be((byte)PaymentStatus.Paid);
        (await verify.PaymentCallbacks.CountAsync(x => x.PaymentId == payment.Id)).Should().Be(1);
        gateways.Sum(x => x.VerifyCount).Should().Be(1);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Failed_gateway_verification_leaves_order_unpaid_with_no_financial_side_effects()
    {
        // Resilience (Part 8): when the payment gateway reports verification failure, the order must
        // stay unpaid, the payment must not be marked Paid, and NO wallet debit/credit may occur.
        // NullWallet throws on any balance operation, so a stray wallet call would fail this test.
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var authority = $"FAILVERIFY-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var result = await NewPaymentService(db, new FailingGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");
            result.IsPaid.Should().BeFalse();
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.Payments.SingleAsync(x => x.Id == payment.Id)).Status.Should().NotBe((byte)PaymentStatus.Paid);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Wallet_refund_is_atomic_idempotent_and_financially_audited()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var order = NewOrder(user.Id);
        order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var payment = NewPayment(user.Id, order.Id, $"REFUND-{Guid.NewGuid():N}");
        payment.Status = (byte)PaymentStatus.Paid; payment.VerifiedAt = DateTime.UtcNow;
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }
        var request = new PaymentRefundRequestDto
        {
            Method = (byte)PaymentRefundMethod.Wallet, Reason = "Integration refund",
            IdempotencyKey = $"refund-{Guid.NewGuid():N}"
        };
        Guid refundId;
        await using (var db = _fixture.CreateDbContext())
        {
            var wallet = new WalletService(db, new NullNotifications());
            var service = NewPaymentService(db, new SuccessfulGateway(), wallet);
            var first = await service.RefundAsync(payment.Id, admin.Id, request);
            var replay = await service.RefundAsync(payment.Id, admin.Id, request);
            replay.Id.Should().Be(first.Id);
            refundId = first.Id;
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.PaymentRefunds.CountAsync(x => x.PaymentId == payment.Id)).Should().Be(1);
        (await verify.PaymentRefunds.SingleAsync(x => x.Id == refundId)).Status.Should().Be((byte)PaymentRefundStatus.Completed);
        (await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync()).Should().Be(order.FinalAmount);
        (await verify.Payments.SingleAsync(x => x.Id == payment.Id)).Status.Should().Be((byte)PaymentStatus.Refunded);
        (await verify.FinancialAuditLogs.Where(x => x.CorrelationId == order.Id).ToListAsync())
            .Should().Contain(x => x.EventType == "PaymentRefundCompleted");
    }

    [Fact]
    public async Task Fulfillment_failure_after_verified_payment_compensates_to_wallet()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.Instant);
        var order = NewOrder(user.Id);
        var item = NewOrderItem(order.Id, product, DeliveryType.Instant);
        var payment = NewPayment(user.Id, order.Id, $"COMP-{Guid.NewGuid():N}");
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product); seed.Orders.Add(order);
            seed.OrderItems.Add(item); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var wallet = new WalletService(db, new NullNotifications());
            var result = await NewPaymentService(db, new SuccessfulGateway(), wallet)
                .VerifyZarinpalPaymentAsync(payment.Authority!, "OK");
            result.IsPaid.Should().BeFalse();
            result.PaymentStatus.Should().Be((byte)PaymentStatus.Refunded);
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleAsync()).Should().Be(100m);
        (await verify.PaymentRefunds.CountAsync(x => x.PaymentId == payment.Id)).Should().Be(1);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Refunded);
    }

    [Fact]
    public async Task Manual_delivery_is_encrypted_single_use_visible_to_owner_and_audited()
    {
        var (user, userToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.Manual);
        var order = NewOrder(user.Id); order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var item = NewOrderItem(order.Id, product, DeliveryType.Manual);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product); seed.Orders.Add(order); seed.OrderItems.Add(item);
            await seed.SaveChangesAsync();
        }
        using var serviceScope = _fixture.Factory.Services.CreateScope();
        var crypto = serviceScope.ServiceProvider.GetRequiredService<IEncryptionService>();
        await using (var db = _fixture.CreateDbContext())
        {
            var service = new OrderService(db, new NullNotifications(), crypto);
            await service.DeliverManualAsync(order.Id, admin.Id,
                new ManualDeliveryRequestDto
                {
                    OrderItemId = item.Id, Content = "private delivery value", IsVisibleToCustomer = true
                });
            Func<Task> act = () => service.DeliverManualAsync(order.Id, admin.Id,
                new ManualDeliveryRequestDto { OrderItemId = item.Id, Content = "duplicate" });
            await act.Should().ThrowAsync<Exception>();
        }

        await using (var verify = _fixture.CreateDbContext())
        {
            var delivery = await verify.OrderItemDeliveries.SingleAsync(x => x.OrderItemId == item.Id);
            delivery.DeliveredContent.Should().NotBe("private delivery value");
            crypto.Decrypt(delivery.DeliveredContent!).Should().Be("private delivery value");
            delivery.EncryptionVersion.Should().Be(2);
            (await verify.FinancialAuditLogs.Where(x => x.CorrelationId == order.Id).ToListAsync())
                .Should().Contain(x => x.EventType == "ManualDeliveryCompleted");
        }

        using var customer = _fixture.CreateClient(userToken);
        var library = await customer.GetAsync("/api/orders/deliveries");
        library.EnsureSuccessStatusCode();
        (await library.Content.ReadAsStringAsync()).Should().Contain("private delivery value");
    }

    [Fact]
    public async Task Gift_delivery_is_encrypted_idempotent_completes_order_and_records_history()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.Instant);
        var order = NewOrder(user.Id);
        order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var item = NewOrderItem(order.Id, product, DeliveryType.Instant);
        var gift = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id,
            EncryptedCode = crypto.Encrypt("GIFT-INTEGRATION-SECRET"), MaskedCode = "****CRET",
            CodeHashFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes("GIFT-INTEGRATION-SECRET"))),
            EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Sold, ReservedByUserId = user.Id,
            ReservedAt = DateTime.UtcNow.AddMinutes(-1), SoldAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow
        };
        var reservation = new GiftCodeReservation
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, OrderItemId = item.Id,
            ProductId = product.Id, GiftCodeId = gift.Id, Status = (byte)GiftCodeReservationStatus.Sold,
            ReservedAt = DateTime.UtcNow.AddMinutes(-1), ExpiresAt = DateTime.UtcNow.AddMinutes(10), SoldAt = DateTime.UtcNow
        };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product); seed.Orders.Add(order);
            seed.OrderItems.Add(item); seed.GiftCodes.Add(gift); seed.GiftCodeReservations.Add(reservation);
            await seed.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var service = new GiftCodeDeliveryService(db, crypto);
            await service.DeliverOrderAsync(order.Id);
            await service.DeliverOrderAsync(order.Id);
        }
        await using var verify = _fixture.CreateDbContext();
        var delivery = await verify.OrderItemDeliveries.SingleAsync(x => x.OrderItemId == item.Id);
        delivery.DeliveredContent.Should().NotContain("GIFT-INTEGRATION-SECRET");
        crypto.Decrypt(delivery.DeliveredContent!).Should().Be("GIFT-INTEGRATION-SECRET");
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Completed);
        (await verify.GiftCodes.SingleAsync(x => x.Id == gift.Id)).Status.Should().Be((byte)GiftCodeStatus.Delivered);
        (await verify.OrderStatusHistories.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
        (await verify.FinancialAuditLogs.CountAsync(x => x.CorrelationId == order.Id && x.EventType == "GiftCodeDelivered"))
            .Should().Be(1);
    }

    private PaymentService NewPaymentService(Vitorize.Infrastructure.Persistence.VitorizeDbContext db,
        IZarinpalGatewayService gateway, IWalletService wallet) =>
        new(db, new NullGiftDelivery(), new NullCoupon(), wallet, new NullNotifications(), gateway, new NullSmsOutbox());

    private static AesEncryptionService Crypto() => new(Options.Create(new Vitorize.Application.Common.EncryptionSettings
        { Key = "0123456789abcdef0123456789abcdef" }));

    private static Order NewOrder(Guid userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"VT-PAY-{Guid.NewGuid():N}",
        Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
        SubtotalAmount = 100m, FinalAmount = 100m, CreatedAt = DateTime.UtcNow
    };
    private static Payment NewPayment(Guid userId, Guid orderId, string authority) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderId = orderId, Amount = 100m,
        Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending,
        RequestedAt = DateTime.UtcNow
    };
    private static Category NewCategory() => new()
    {
        Id = Guid.NewGuid(), Title = "Delivery", Slug = $"delivery-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow
    };
    private static Product NewProduct(Guid categoryId, DeliveryType delivery) => new()
    {
        Id = Guid.NewGuid(), CategoryId = categoryId, Title = "Delivery product", Slug = $"delivery-product-{Guid.NewGuid():N}",
        ProductType = (byte)ProductType.Other, DeliveryType = (byte)delivery, BasePrice = 100m,
        CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
    };
    private static OrderItem NewOrderItem(Guid orderId, Product product, DeliveryType delivery) => new()
    {
        Id = Guid.NewGuid(), OrderId = orderId, ProductId = product.Id, ProductTitle = product.Title,
        Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, DeliveryType = (byte)delivery,
        DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
    };

    private sealed class SuccessfulGateway : IZarinpalGatewayService
    {
        public int VerifyCount { get; private set; }
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((true, $"A-{Guid.NewGuid():N}", "https://payment.test"));
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string authority, decimal amount)
        { VerifyCount++; return Task.FromResult((true, 12345L)); }
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult("https://payment.test");
    }
    private sealed class FailingGateway : IZarinpalGatewayService
    {
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((false, string.Empty, string.Empty));
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string authority, decimal amount) =>
            Task.FromResult((false, 0L));
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult(string.Empty);
    }
    private sealed class NullGiftDelivery : IGiftCodeDeliveryService { public Task DeliverOrderAsync(Guid orderId, Guid? deliveredByUserId = null) => Task.CompletedTask; }
    private sealed class NullCoupon : ICouponService
    {
        public Task<ValidateCouponResultDto> ValidateAsync(Guid userId, ValidateCouponRequestDto request) => throw new NotSupportedException();
        public Task MarkCouponAsUsedAsync(Guid userId, Guid orderId, Guid couponId) => Task.CompletedTask;
    }
    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message) => Task.CompletedTask;
        public Task<List<NotificationDto>> GetMyNotificationsAsync(Guid userId) => Task.FromResult(new List<NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userId, Guid notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
    }
    private sealed class NullWallet : IWalletService
    {
        public Task<WalletDto> CreditAsync(Guid userId, decimal amount, byte? referenceType, Guid? referenceId, string? description) => throw new NotSupportedException();
        public Task<WalletDto> DebitAsync(Guid userId, decimal amount, byte? referenceType, Guid? referenceId, string? description) => throw new NotSupportedException();
        public Task<WalletDto> GetMyWalletAsync(Guid userId) => throw new NotSupportedException();
        public Task<List<WalletTransactionDto>> GetMyTransactionsAsync(Guid userId) => throw new NotSupportedException();
        public Task<WalletDto> GetUserWalletAsync(Guid userId) => throw new NotSupportedException();
        public Task<List<WalletTransactionDto>> GetUserTransactionsAsync(Guid userId) => throw new NotSupportedException();
        public Task<WalletDto> AdminChargeAsync(WalletChargeRequestDto request) => throw new NotSupportedException();
        public Task<WalletDto> AdminWithdrawAsync(WalletWithdrawRequestDto request) => throw new NotSupportedException();
    }
    private sealed class NullSmsOutbox : ISmsOutboxEnqueuer
    {
        public Task EnqueueTemplateAsync(string? mobile, string templateKey, IReadOnlyList<SmsTemplateParameter> parameters, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) => Task.CompletedTask;
        public Task EnqueueTextAsync(string? mobile, string text, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) => Task.CompletedTask;
    }
}
