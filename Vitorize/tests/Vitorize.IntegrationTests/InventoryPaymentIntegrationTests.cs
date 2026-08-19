using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.DTOs.Wallet;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Managed-inventory consumption driven through the REAL PaymentService against real SQL Server.
///
/// InventoryConsumptionIntegrationTests proves the SQL statement is atomic and non-negative, and
/// deliberately demonstrates that replaying that statement decrements again. Exactly-once therefore
/// rests on PaymentService's canonical paid-state guard, which is what these tests certify: every
/// success path funnels through CompletePaidOrderAsync, which returns early once PaymentStatus is
/// Paid, and the decrement commits inside the caller's transaction.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class InventoryPaymentIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public InventoryPaymentIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- fixture helpers

    private sealed record Sku(Guid ProductId, Guid VariantId);

    private async Task<Sku> CreateSkuAsync(DeliveryType delivery, int stock)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "inv", Slug = $"inv-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "inv product",
            Slug = $"inv-p-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)delivery, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman,
            MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var variant = new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "v", Price = 100m,
            StockMode = (byte)(delivery == DeliveryType.Instant
                ? ProductVariantStockMode.GiftCode
                : ProductVariantStockMode.Manual),
            StockQuantity = stock, IsDefault = true, IsActive = true, SortOrder = 0,
            CreatedAt = DateTime.UtcNow
        };
        db.Categories.Add(category);
        db.Products.Add(product);
        db.ProductVariants.Add(variant);
        await db.SaveChangesAsync();
        return new Sku(product.Id, variant.Id);
    }

    private async Task<(Order Order, Payment Payment, string Authority)> CreatePendingOrderAsync(
        Guid userId, Sku sku, DeliveryType delivery, int quantity, decimal finalAmount = 100m)
    {
        var authority = $"AUTH-{Guid.NewGuid():N}";
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"VT-INV-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            SubtotalAmount = finalAmount, FinalAmount = finalAmount,
            CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow
        };
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = sku.ProductId,
            ProductVariantId = sku.VariantId, ProductTitle = "inv product",
            Quantity = quantity, UnitPrice = 100m, TotalPrice = 100m * quantity,
            DeliveryType = (byte)delivery, DeliveryStatus = (byte)DeliveryStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        var payment = new Payment
        {
            Id = Guid.NewGuid(), UserId = userId, OrderId = order.Id, Amount = finalAmount,
            Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending,
            CurrencyType = (byte)CurrencyType.Toman, RequestedAt = DateTime.UtcNow
        };

        await using var db = _fixture.CreateDbContext();
        db.Orders.Add(order);
        db.OrderItems.Add(item);
        db.Payments.Add(payment);
        await db.SaveChangesAsync();
        return (order, payment, authority);
    }

    private async Task<int> StockAsync(Guid variantId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.ProductVariants.Where(x => x.Id == variantId)
            .Select(x => x.StockQuantity).SingleAsync();
    }

    private async Task<(byte PaymentStatus, byte OrderStatus)> OrderStateAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        var o = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == orderId);
        return (o.PaymentStatus, o.Status);
    }

    private async Task<int> AuditCountAsync(Guid orderId, string eventType)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.FinancialAuditLogs
            .CountAsync(x => x.CorrelationId == orderId && x.EventType == eventType);
    }

    // ---------------------------------------------------------------- gateway

    [Fact]
    public async Task Gateway_success_consumes_managed_stock_exactly_once()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(3, "quantity 2 of 5 must be consumed");
        var state = await OrderStateAsync(order.Id);
        state.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        state.OrderStatus.Should().Be((byte)OrderStatus.Processing);
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
        (await AuditCountAsync(order.Id, "StockShortfall")).Should().Be(0);
    }

    [Fact]
    public async Task Gateway_failure_leaves_managed_stock_untouched()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new FailingGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(5);
        (await OrderStateAsync(order.Id)).PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(0);
    }

    [Fact]
    public async Task Duplicate_gateway_callback_decrements_managed_stock_only_once()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = _fixture.CreateDbContext();
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");
        }

        // The persisted value is the assertion, not a return code.
        (await StockAsync(sku.VariantId)).Should().Be(3, "the paid guard must suppress the second consumption");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_duplicate_callbacks_decrement_managed_stock_only_once()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");
        }));

        (await StockAsync(sku.VariantId)).Should().Be(3);
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
    }

    // ---------------------------------------------------------------- reconciliation

    [Fact]
    public async Task Reconciliation_after_a_completed_callback_does_not_consume_again()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(3);

        for (var run = 0; run < 2; run++)
        {
            await using var db = _fixture.CreateDbContext();
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .ReconcilePendingZarinpalPaymentsAsync();
        }

        (await StockAsync(sku.VariantId)).Should().Be(3, "reconciliation must not double-consume");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
    }

    // ---------------------------------------------------------------- wallet

    [Fact]
    public async Task Wallet_success_consumes_managed_stock_once_and_replay_does_not()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, _) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        await using (var db = _fixture.CreateDbContext())
            await new WalletService(db, new NullNotifications()).CreditAsync(
                user.Id, 1000m, (byte)WalletReferenceType.Cashback, Guid.NewGuid(), "inventory test funding");

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new WalletService(db, new NullNotifications()))
                .PayWithWalletAsync(user.Id, order.Id);

        (await StockAsync(sku.VariantId)).Should().Be(3);
        (await OrderStateAsync(order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);

        // Replay the same logical completion.
        await using (var db = _fixture.CreateDbContext())
        {
            var replay = async () => await NewPaymentService(
                    db, new SuccessfulGateway(), new WalletService(db, new NullNotifications()))
                .PayWithWalletAsync(user.Id, order.Id);
            // Whether the service rejects the replay or short-circuits, stock must not move again.
            try { await replay(); } catch { /* an already-paid order may legitimately be refused */ }
        }

        (await StockAsync(sku.VariantId)).Should().Be(3, "wallet replay must not decrement twice");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
    }

    // ---------------------------------------------------------------- coupon

    [Fact]
    public async Task A_discount_does_not_change_how_many_units_are_consumed()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);

        // Heavily discounted order: the payable amount is small, the quantity is still 2.
        var (order, _, authority) = await CreatePendingOrderAsync(
            user.Id, sku, DeliveryType.Manual, quantity: 2, finalAmount: 20m);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(3, "consumption follows OrderItem.Quantity, not money");
    }

    // ---------------------------------------------------------------- multiple variants

    [Fact]
    public async Task Multiple_managed_variants_in_one_order_are_each_consumed_correctly()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var a = await CreateSkuAsync(DeliveryType.Manual, stock: 10);
        var b = await CreateSkuAsync(DeliveryType.SupportRequired, stock: 5);

        var authority = $"AUTH-{Guid.NewGuid():N}";
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"VT-INV-{Guid.NewGuid():N}",
            Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
            SubtotalAmount = 500m, FinalAmount = 500m, CurrencyType = (byte)CurrencyType.Toman,
            CreatedAt = DateTime.UtcNow
        };
        await using (var db = _fixture.CreateDbContext())
        {
            db.Orders.Add(order);
            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(), OrderId = order.Id, ProductId = a.ProductId, ProductVariantId = a.VariantId,
                ProductTitle = "A", Quantity = 3, UnitPrice = 100m, TotalPrice = 300m,
                DeliveryType = (byte)DeliveryType.Manual, DeliveryStatus = (byte)DeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            db.OrderItems.Add(new OrderItem
            {
                Id = Guid.NewGuid(), OrderId = order.Id, ProductId = b.ProductId, ProductVariantId = b.VariantId,
                ProductTitle = "B", Quantity = 2, UnitPrice = 100m, TotalPrice = 200m,
                DeliveryType = (byte)DeliveryType.SupportRequired, DeliveryStatus = (byte)DeliveryStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            db.Payments.Add(new Payment
            {
                Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, Amount = 500m,
                Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending,
                CurrencyType = (byte)CurrencyType.Toman, RequestedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(a.VariantId)).Should().Be(7);
        (await StockAsync(b.VariantId)).Should().Be(3);
    }

    // ---------------------------------------------------------------- shortfall

    [Fact]
    public async Task A_payment_confirmed_after_the_last_unit_is_gone_is_preserved_and_flagged()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 1);

        var first = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 1);
        var second = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 1);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(first.Authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(0);

        // The second buyer's payment is independently confirmed after stock ran out.
        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(second.Authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(0, "stock must never go negative");

        var state = await OrderStateAsync(second.Order.Id);
        state.PaymentStatus.Should().Be((byte)PaymentStatus.Paid, "a confirmed payment is never discarded");
        state.OrderStatus.Should().Be((byte)OrderStatus.Processing, "the order stays in the queue admins work");

        (await AuditCountAsync(second.Order.Id, "StockShortfall")).Should().Be(1);
        (await AuditCountAsync(second.Order.Id, "StockConsumed")).Should().Be(0);
    }

    [Fact]
    public async Task Replaying_a_shortfall_payment_does_not_multiply_the_audit_trail()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 1);
        var first = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 1);
        var second = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 1);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(first.Authority, "OK");

        for (var attempt = 0; attempt < 2; attempt++)
        {
            await using var db = _fixture.CreateDbContext();
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(second.Authority, "OK");
        }

        (await StockAsync(sku.VariantId)).Should().Be(0);
        (await AuditCountAsync(second.Order.Id, "StockShortfall"))
            .Should().Be(1, "the paid guard must suppress the replayed shortfall too");
    }

    // ---------------------------------------------------------------- instant regression

    [Fact]
    public async Task Instant_delivery_never_touches_the_dormant_managed_quantity()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        // A deliberately large dormant quantity on an Instant SKU: it must be completely inert.
        var sku = await CreateSkuAsync(DeliveryType.Instant, stock: 100);

        await using (var db = _fixture.CreateDbContext())
        {
            db.GiftCodes.Add(new GiftCode
            {
                Id = Guid.NewGuid(), ProductId = sku.ProductId, ProductVariantId = sku.VariantId,
                EncryptedCode = "ignored-by-this-test", Status = (byte)GiftCodeStatus.Available,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Instant, quantity: 1);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        (await StockAsync(sku.VariantId)).Should().Be(100, "Instant inventory is the gift-code pool");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(0);
        (await AuditCountAsync(order.Id, "StockShortfall")).Should().Be(0);
    }

    // ---------------------------------------------------------------- reconciliation as first success

    [Fact]
    public async Task Reconciliation_as_the_first_authoritative_success_consumes_stock_once()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, payment, _) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        // Reconciliation only sweeps payments older than PendingPaymentReconciliationAgeMinutes, so
        // age this one to make it genuinely eligible rather than weakening the production filter.
        await using (var db = _fixture.CreateDbContext())
        {
            var tracked = await db.Payments.SingleAsync(x => x.Id == payment.Id);
            tracked.RequestedAt = DateTime.UtcNow.AddHours(-2);
            await db.SaveChangesAsync();
        }

        // No callback has completed this payment; reconciliation is the first authoritative success.
        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .ReconcilePendingZarinpalPaymentsAsync();

        (await StockAsync(sku.VariantId)).Should().Be(3);
        var state = await OrderStateAsync(order.Id);
        state.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        state.OrderStatus.Should().Be((byte)OrderStatus.Processing);

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .ReconcilePendingZarinpalPaymentsAsync();

        (await StockAsync(sku.VariantId)).Should().Be(3, "a second reconciliation must not consume again");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(1);
    }

    // ---------------------------------------------------------------- wallet failure

    [Fact]
    public async Task A_wallet_payment_that_cannot_complete_leaves_stock_untouched()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, _) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        // The wallet is deliberately unfunded, so the real service refuses the payment.
        await using (var db = _fixture.CreateDbContext())
        {
            var pay = async () => await NewPaymentService(
                    db, new SuccessfulGateway(), new WalletService(db, new NullNotifications()))
                .PayWithWalletAsync(user.Id, order.Id);
            await pay.Should().ThrowAsync<Exception>("an unfunded wallet cannot pay");
        }

        (await StockAsync(sku.VariantId)).Should().Be(5);
        (await OrderStateAsync(order.Id)).PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(0);
        (await AuditCountAsync(order.Id, "StockShortfall")).Should().Be(0);
    }

    // ---------------------------------------------------------------- transaction rollback

    [Fact]
    public async Task A_failure_after_consumption_rolls_back_both_the_stock_and_the_paid_transition()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var sku = await CreateSkuAsync(DeliveryType.Manual, stock: 5);
        var (order, _, authority) = await CreatePendingOrderAsync(user.Id, sku, DeliveryType.Manual, quantity: 2);

        // The SMS enqueue runs inside CompletePaidOrderAsync AFTER the decrement and BEFORE the caller
        // commits, so throwing there is a production-equivalent mid-transaction failure that needs no
        // test hook in production code.
        await using (var db = _fixture.CreateDbContext())
        {
            var service = new PaymentService(db, new GiftCodeDeliveryService(db, Crypto()), new NullCoupon(),
                new NullWallet(), new NullNotifications(), new SuccessfulGateway(), new ThrowingSmsOutbox());
            var act = async () => await service.VerifyZarinpalPaymentAsync(authority, "OK");
            await act.Should().ThrowAsync<Exception>();
        }

        (await StockAsync(sku.VariantId)).Should().Be(5, "the decrement must roll back with the transaction");
        (await OrderStateAsync(order.Id)).PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid,
            "the paid transition must roll back with the stock");
        (await AuditCountAsync(order.Id, "StockConsumed")).Should().Be(0);
    }

    // ---------------------------------------------------------------- delivery-type transitions

    [Theory]
    [InlineData(DeliveryType.Manual, 20)]
    [InlineData(DeliveryType.SupportRequired, 7)]
    public async Task Managed_quantity_survives_a_round_trip_through_instant_delivery(DeliveryType original, int stock)
    {
        var sku = await CreateSkuAsync(original, stock);

        async Task SetDeliveryTypeAsync(DeliveryType target)
        {
            await using var db = _fixture.CreateDbContext();
            var product = await db.Products.SingleAsync(x => x.Id == sku.ProductId);
            product.DeliveryType = (byte)target;
            var variant = await db.ProductVariants.SingleAsync(x => x.Id == sku.VariantId);
            variant.StockMode = (byte)Vitorize.Application.Common.ProductAvailabilityRules
                .RequiredStockMode((byte)target);
            await db.SaveChangesAsync();
        }

        async Task<(int Stock, byte Mode)> ReadAsync()
        {
            await using var db = _fixture.CreateDbContext();
            var v = await db.ProductVariants.AsNoTracking().SingleAsync(x => x.Id == sku.VariantId);
            return (v.StockQuantity, v.StockMode);
        }

        // -> Instant: the quantity goes dormant but must not be destroyed.
        await SetDeliveryTypeAsync(DeliveryType.Instant);
        var afterInstant = await ReadAsync();
        afterInstant.Stock.Should().Be(stock, "switching to Instant must never erase managed inventory");
        afterInstant.Mode.Should().Be((byte)ProductVariantStockMode.GiftCode);
        Vitorize.Application.Common.ProductAvailabilityRules
            .AvailableUnits((byte)DeliveryType.Instant, availableGiftCodes: 0, stockQuantity: afterInstant.Stock)
            .Should().Be(0, "the dormant value must be inert while Instant");

        // -> back: the preserved quantity becomes authoritative again.
        await SetDeliveryTypeAsync(original);
        var restored = await ReadAsync();
        restored.Stock.Should().Be(stock);
        restored.Mode.Should().Be((byte)ProductVariantStockMode.Manual);
        Vitorize.Application.Common.ProductAvailabilityRules
            .AvailableUnits((byte)original, 0, restored.Stock).Should().Be(stock);
    }

    // ---------------------------------------------------------------- harness (mirrors PaymentDeliveryIntegrationTests)

    private PaymentService NewPaymentService(Vitorize.Infrastructure.Persistence.VitorizeDbContext db,
        IZarinpalGatewayService gateway, IWalletService wallet)
    {
        var notifications = new NullNotifications();
        var giftDelivery = new GiftCodeDeliveryService(db, Crypto());
        var processor = new PostPaymentOrderProcessor(
            db, new PaidGiftCodeAllocationService(db), giftDelivery, notifications);
        return new PaymentService(db, giftDelivery, new NullCoupon(), wallet, notifications, gateway,
            new NullSmsOutbox(), postPaymentOrderProcessor: processor);
    }

    private static AesEncryptionService Crypto() => new(Options.Create(
        new Vitorize.Application.Common.EncryptionSettings { Key = "0123456789abcdef0123456789abcdef" }));

    private sealed class SuccessfulGateway : IZarinpalGatewayService
    {
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, CurrencyType currency, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((true, $"A-{Guid.NewGuid():N}", "https://payment.test"));
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string authority, decimal amount) =>
            Task.FromResult((true, 12345L));
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult("https://payment.test");
    }

    private sealed class FailingGateway : IZarinpalGatewayService
    {
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, CurrencyType currency, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((false, string.Empty, string.Empty));
        public Task<(bool Success, long RefId)> VerifyPaymentAsync(string authority, decimal amount) =>
            Task.FromResult((false, 0L));
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult(string.Empty);
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

    private sealed class NullCoupon : ICouponService
    {
        public Task<ValidateCouponResultDto> ValidateAsync(Guid userId, ValidateCouponRequestDto request) => throw new NotSupportedException();
        public Task MarkCouponAsUsedAsync(Guid userId, Guid orderId, Guid couponId) => Task.CompletedTask;
    }

    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message) => Task.CompletedTask;
        public Task<int> CreateBulkAsync(Guid broadcastId, IReadOnlyCollection<Guid> recipientUserIds, string title, string message, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<List<NotificationDto>> GetMyNotificationsAsync(Guid userId) => Task.FromResult(new List<NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userId, Guid notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
    }

    /// <summary>Fails inside CompletePaidOrderAsync, after the decrement and before the commit.</summary>
    private sealed class ThrowingSmsOutbox : ISmsOutboxEnqueuer
    {
        public Task EnqueueTemplateAsync(string? mobile, string templateKey, IReadOnlyList<SmsTemplateParameter> parameters, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) =>
            throw new InvalidOperationException("injected mid-transaction failure");
        public Task EnqueueTextAsync(string? mobile, string text, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) =>
            throw new InvalidOperationException("injected mid-transaction failure");
    }

    private sealed class NullSmsOutbox : ISmsOutboxEnqueuer
    {
        public Task EnqueueTemplateAsync(string? mobile, string templateKey, IReadOnlyList<SmsTemplateParameter> parameters, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) => Task.CompletedTask;
        public Task EnqueueTextAsync(string? mobile, string text, string purpose, Guid? aggregateId, CancellationToken cancellationToken = default, Guid? userId = null, Guid? createdByUserId = null, string? relatedEntityType = null, string? relatedEntityReference = null, string? idempotencyKey = null, string? note = null) => Task.CompletedTask;
    }
}
