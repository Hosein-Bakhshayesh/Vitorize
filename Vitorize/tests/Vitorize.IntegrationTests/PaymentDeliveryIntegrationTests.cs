using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Admin.Payments;
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
using Vitorize.Shared.Exceptions;

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
    public async Task Failed_attempt_is_preserved_and_retry_creates_one_new_paid_attempt_for_the_same_order()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var failed = NewPayment(user.Id, order.Id, $"FAILED-{Guid.NewGuid():N}");
        failed.Status = (byte)PaymentStatus.Failed;
        failed.ProviderStatusCode = "REQUEST_FAILED";
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(failed); await seed.SaveChangesAsync();
        }

        PaymentStartResultDto started;
        await using (var db = _fixture.CreateDbContext())
        {
            var service = NewPaymentService(db, new SuccessfulGateway(), new NullWallet());
            started = await service.StartPaymentAsync(user.Id, order.Id);
            started.PaymentId.Should().NotBe(failed.Id);
            await service.VerifyMockPaymentAsync(user.Id, started.PaymentId);
        }

        await using var verify = _fixture.CreateDbContext();
        var attempts = await verify.Payments.Where(x => x.OrderId == order.Id).OrderBy(x => x.RequestedAt).ToListAsync();
        attempts.Should().HaveCount(2);
        attempts.Single(x => x.Id == failed.Id).Status.Should().Be((byte)PaymentStatus.Failed);
        attempts.Single(x => x.Id == started.PaymentId).Status.Should().Be((byte)PaymentStatus.Paid);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Provider_request_failure_keeps_order_retryable_and_a_later_attempt_succeeds()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var ready = NewPayment(user.Id, order.Id, string.Empty);
        ready.Authority = null; ready.ProviderStatusCode = "READY";
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(ready); await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
        {
            var failing = NewPaymentService(db, new FailingGateway(), new NullWallet());
            Func<Task> start = () => failing.StartPaymentAsync(user.Id, order.Id);
            await start.Should().ThrowAsync<BusinessException>();
        }
        await using (var check = _fixture.CreateDbContext())
        {
            (await check.Payments.SingleAsync(x => x.Id == ready.Id)).Status.Should().Be((byte)PaymentStatus.Failed);
            var eligibility = await NewPaymentService(check, new SuccessfulGateway(), new NullWallet())
                .GetRetryEligibilityAsync(user.Id, order.Id);
            eligibility.CanRetry.Should().BeTrue();
        }
        await using (var retryDb = _fixture.CreateDbContext())
        {
            var service = NewPaymentService(retryDb, new SuccessfulGateway(), new NullWallet());
            var retry = await service.StartPaymentAsync(user.Id, order.Id);
            await service.VerifyMockPaymentAsync(user.Id, retry.PaymentId);
        }
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verify.Payments.CountAsync(x => x.OrderId == order.Id)).Should().Be(2);
    }

    [Fact]
    public async Task Cancelled_or_verify_failed_gateway_attempt_can_be_retried_without_creating_another_order()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var cancelled = NewPayment(user.Id, order.Id, $"CANCEL-{Guid.NewGuid():N}");
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(cancelled); await seed.SaveChangesAsync();
        }
        await using (var callbackDb = _fixture.CreateDbContext())
            await NewPaymentService(callbackDb, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(cancelled.Authority!, "NOK");
        await using (var retryDb = _fixture.CreateDbContext())
        {
            var service = NewPaymentService(retryDb, new SuccessfulGateway(), new NullWallet());
            var retry = await service.StartPaymentAsync(user.Id, order.Id);
            await service.VerifyMockPaymentAsync(user.Id, retry.PaymentId);
        }
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(1);
        (await verify.Payments.SingleAsync(x => x.Id == cancelled.Id)).Status.Should().Be((byte)PaymentStatus.Cancelled);
        (await verify.Payments.CountAsync(x => x.OrderId == order.Id && x.Status == (byte)PaymentStatus.Paid)).Should().Be(1);
    }

    [Fact]
    public async Task Stale_pending_attempt_is_preserved_as_expired_and_replaced()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var stale = NewPayment(user.Id, order.Id, $"STALE-{Guid.NewGuid():N}");
        stale.RequestedAt = DateTime.UtcNow.AddMinutes(-31);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(stale); await seed.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var retry = await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .StartPaymentAsync(user.Id, order.Id);
            retry.PaymentId.Should().NotBe(stale.Id);
        }
        await using var verify = _fixture.CreateDbContext();
        (await verify.Payments.SingleAsync(x => x.Id == stale.Id)).ProviderStatusCode.Should().Be("ATTEMPT_EXPIRED");
        (await verify.Payments.SingleAsync(x => x.Id == stale.Id)).Status.Should().Be((byte)PaymentStatus.Failed);
        (await verify.Payments.CountAsync(x => x.OrderId == order.Id && x.Status == (byte)PaymentStatus.Pending)).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_payment_starts_create_only_one_external_authority()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var ready = NewPayment(user.Id, order.Id, string.Empty);
        ready.Authority = null; ready.ProviderStatusCode = "READY";
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(ready); await seed.SaveChangesAsync();
        }
        var gateway = new SlowSuccessfulGateway();
        async Task<bool> StartAsync()
        {
            await using var db = _fixture.CreateDbContext();
            try
            {
                await NewPaymentService(db, gateway, new NullWallet()).StartPaymentAsync(user.Id, order.Id);
                return true;
            }
            catch (BusinessException)
            {
                return false;
            }
        }

        var results = await Task.WhenAll(StartAsync(), StartAsync());
        results.Count(x => x).Should().Be(1);
        gateway.CreateCount.Should().Be(1);
        await using var verify = _fixture.CreateDbContext();
        (await verify.Payments.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
        (await verify.Payments.SingleAsync(x => x.Id == ready.Id)).Authority.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Retry_denies_other_customer_and_is_forbidden_after_a_successful_payment()
    {
        var (owner, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (other, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(owner.Id);
        var failed = NewPayment(owner.Id, order.Id, $"FAILED-{Guid.NewGuid():N}");
        failed.Status = (byte)PaymentStatus.Failed;
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(failed); await seed.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var service = NewPaymentService(db, new SuccessfulGateway(), new NullWallet());
            Func<Task> idor = () => service.StartPaymentAsync(other.Id, order.Id);
            await idor.Should().ThrowAsync<NotFoundException>();
            var started = await service.StartPaymentAsync(owner.Id, order.Id);
            await service.VerifyMockPaymentAsync(owner.Id, started.PaymentId);
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var service = NewPaymentService(db, new SuccessfulGateway(), new NullWallet());
            Func<Task> retryPaid = () => service.StartPaymentAsync(owner.Id, order.Id);
            await retryPaid.Should().ThrowAsync<BusinessException>();
        }
    }

    [Fact]
    public async Task Late_successful_cancelled_attempt_is_financially_flagged_after_a_newer_attempt_paid()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        order.Status = (byte)OrderStatus.Processing;
        order.PaymentStatus = (byte)PaymentStatus.Paid;
        var old = NewPayment(user.Id, order.Id, $"OLD-{Guid.NewGuid():N}");
        old.Status = (byte)PaymentStatus.Cancelled;
        var current = NewPayment(user.Id, order.Id, $"NEW-{Guid.NewGuid():N}");
        current.Status = (byte)PaymentStatus.Paid;
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.AddRange(old, current); await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(old.Authority!, "OK");

        await using var verify = _fixture.CreateDbContext();
        var late = await verify.Payments.SingleAsync(x => x.Id == old.Id);
        late.Status.Should().Be((byte)PaymentStatus.Failed);
        late.ProviderStatusCode.Should().Be("LATE_SUCCESS_REQUIRES_FINANCE");
        (await verify.FinancialAuditLogs.CountAsync(x => x.EntityId == old.Id &&
            x.EventType == "LateGatewayPaymentRequiresFinanceResolution")).Should().Be(1);
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
    public async Task Pending_manual_refund_rejects_a_second_idempotency_key()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var order = NewOrder(user.Id);
        order.Status = (byte)OrderStatus.Processing; order.PaymentStatus = (byte)PaymentStatus.Paid;
        var payment = NewPayment(user.Id, order.Id, $"MANUAL-REFUND-{Guid.NewGuid():N}");
        payment.Status = (byte)PaymentStatus.Paid; payment.VerifiedAt = DateTime.UtcNow;
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order); seed.Payments.Add(payment); await seed.SaveChangesAsync();
        }

        await using var db = _fixture.CreateDbContext();
        var service = NewPaymentService(db, new SuccessfulGateway(), new NullWallet());
        var first = await service.RefundAsync(payment.Id, admin.Id, new PaymentRefundRequestDto
        {
            Method = (byte)PaymentRefundMethod.GatewayManual, Reason = "Manual gateway refund",
            IdempotencyKey = $"refund-{Guid.NewGuid():N}"
        });
        first.Status.Should().Be((byte)PaymentRefundStatus.Pending);
        Func<Task> duplicate = () => service.RefundAsync(payment.Id, admin.Id, new PaymentRefundRequestDto
        {
            Method = (byte)PaymentRefundMethod.GatewayManual, Reason = "Duplicate manual refund",
            IdempotencyKey = $"refund-{Guid.NewGuid():N}"
        });
        await duplicate.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Payment_refund_and_financial_audit_histories_are_paged_in_sql()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var order = NewOrder(user.Id);
        var payment = NewPayment(user.Id, order.Id, $"PAGED-DETAIL-{Guid.NewGuid():N}");
        var requestedAt = DateTime.UtcNow.AddMinutes(-55);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order);
            seed.Payments.Add(payment);
            seed.PaymentRefunds.AddRange(Enumerable.Range(1, 55).Select(index => new PaymentRefund
            {
                Id = Guid.NewGuid(), PaymentId = payment.Id, OrderId = order.Id, UserId = user.Id,
                RequestedByUserId = admin.Id, Amount = 1m, Method = (byte)PaymentRefundMethod.GatewayManual,
                Status = (byte)PaymentRefundStatus.Pending, Reason = $"Paged refund {index:000}",
                IdempotencyKey = $"paged-refund-{Guid.NewGuid():N}", RequestedAt = requestedAt.AddMinutes(index)
            }));
            seed.FinancialAuditLogs.AddRange(Enumerable.Range(1, 55).Select(index => new FinancialAuditLog
            {
                EventType = "PagedAudit", EntityType = "Payment", EntityId = payment.Id,
                CorrelationId = order.Id, Amount = index, Detail = $"Paged audit {index:000}",
                CreatedAt = requestedAt.AddMinutes(index)
            }));
            await seed.SaveChangesAsync();
        }

        await using var db = _fixture.CreateDbContext();
        var service = new AdminPaymentReadService(db);
        var header = await service.GetByIdAsync(payment.Id);
        var refunds = await service.GetRefundsPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 1, PageSize = 20, SortDirection = "asc" });
        var refundLast = await service.GetRefundsPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 3, PageSize = 20 });
        var refundsBeyondLast = await service.GetRefundsPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 4, PageSize = 20 });
        var refundsCapped = await service.GetRefundsPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 1, PageSize = 500 });
        var audit = await service.GetAuditHistoryPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 1, PageSize = 20, SortDirection = "asc" });
        var auditLast = await service.GetAuditHistoryPagedAsync(payment.Id, new PaymentDetailHistoryFilterDto { Page = 3, PageSize = 20 });

        header.Refunds.Should().BeEmpty();
        header.AuditHistory.Should().BeEmpty();
        refunds.TotalCount.Should().Be(55); refunds.PageSize.Should().Be(20); refunds.Items.Should().HaveCount(20);
        refundLast.Items.Should().HaveCount(15); refundsBeyondLast.Items.Should().BeEmpty();
        refundsCapped.PageSize.Should().Be(100); refundsCapped.Items.Should().HaveCount(55);
        refunds.Items.Select(x => x.RequestedAt).Should().BeInAscendingOrder();
        audit.TotalCount.Should().Be(55); audit.Items.Should().HaveCount(20); auditLast.Items.Should().HaveCount(15);
        audit.Items.Select(x => x.CreatedAt).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Fulfillment_failure_after_verified_payment_keeps_financial_payment_authoritative()
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
            result.IsPaid.Should().BeTrue();
            result.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.Wallets.Where(x => x.UserId == user.Id).Select(x => x.Balance).SingleOrDefaultAsync()).Should().Be(0m);
        (await verify.PaymentRefunds.CountAsync(x => x.PaymentId == payment.Id)).Should().Be(0);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
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

    // ---- SupportRequired opt-in auto-ticket creation (Product.RequiresSupportMessage) ----

    [Fact]
    public async Task SupportRequired_optin_order_auto_creates_one_customer_visible_ticket_linked_to_order_item()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.SupportRequired);
        product.IsActive = false; // keep this catalog fixture out of the public sitemap
        product.RequiresSupportMessage = true; // opt in
        var order = NewOrder(user.Id);
        var item = NewOrderItem(order.Id, product, DeliveryType.SupportRequired);
        item.VariantTitle = "نسخه استاندارد";
        var authority = $"SUP-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product);
            seed.Orders.Add(order); seed.OrderItems.Add(item); seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        await using var verify = _fixture.CreateDbContext();
        var tickets = await verify.Tickets.Include(x => x.TicketMessages)
            .Where(x => x.OrderId == order.Id).ToListAsync();
        tickets.Should().ContainSingle("a support-delivery opt-in order auto-creates exactly one ticket");
        var ticket = tickets[0];
        ticket.UserId.Should().Be(user.Id);
        ticket.Status.Should().Be((byte)TicketStatus.WaitingForAdmin);
        ticket.TicketMessages.Should().ContainSingle();
        ticket.TicketMessages.Single().IsInternalNote.Should().BeFalse("the first message is customer-visible");
        ticket.TicketMessages.Single().Message.Should().Contain("نسخه استاندارد", "the selected edition is included");
        (await verify.OrderItems.SingleAsync(x => x.Id == item.Id)).SupportTicketId.Should().Be(ticket.Id);
        // No gift-code reservation or allocation for a support-delivered order.
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderId == order.Id)).Should().Be(0);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Duplicate_callbacks_and_reverification_do_not_duplicate_the_support_ticket()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.SupportRequired);
        product.IsActive = false; // keep this catalog fixture out of the public sitemap
        product.RequiresSupportMessage = true;
        var order = NewOrder(user.Id);
        var item = NewOrderItem(order.Id, product, DeliveryType.SupportRequired);
        item.VariantTitle = "نسخه آلتیمیت";
        var authority = $"SUP-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product);
            seed.Orders.Add(order); seed.OrderItems.Add(item); seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }

        // Two concurrent gateway callbacks, then a third sequential re-verification.
        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = _fixture.CreateDbContext();
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");
        }));
        await using (var again = _fixture.CreateDbContext())
            await NewPaymentService(again, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        await using var verify = _fixture.CreateDbContext();
        (await verify.Tickets.CountAsync(x => x.OrderId == order.Id && x.IsFulfillmentTicket)).Should().Be(1, "the fulfilment ticket must be idempotent across retries");
        (await verify.TicketMessages.CountAsync(x => x.Ticket.OrderId == order.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Multi_item_optin_order_creates_one_fulfillment_ticket_without_changing_customer_ticket_or_manual_item()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = NewCategory();
        var firstSupportProduct = NewProduct(category.Id, DeliveryType.SupportRequired);
        var secondSupportProduct = NewProduct(category.Id, DeliveryType.SupportRequired);
        var manualProduct = NewProduct(category.Id, DeliveryType.Manual);
        firstSupportProduct.RequiresSupportMessage = true;
        secondSupportProduct.RequiresSupportMessage = true;
        manualProduct.RequiresSupportMessage = true;
        var order = NewOrder(user.Id);
        var firstSupportItem = NewOrderItem(order.Id, firstSupportProduct, DeliveryType.SupportRequired);
        var secondSupportItem = NewOrderItem(order.Id, secondSupportProduct, DeliveryType.SupportRequired);
        var manualItem = NewOrderItem(order.Id, manualProduct, DeliveryType.Manual);
        var customerTicket = new Ticket
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id,
            Subject = "Customer question remains separate", Department = (byte)TicketDepartment.Orders,
            Priority = (byte)TicketPriority.Normal, Status = (byte)TicketStatus.WaitingForAdmin,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var authority = $"SUP-MULTI-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);

        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category);
            seed.Products.AddRange(firstSupportProduct, secondSupportProduct, manualProduct);
            seed.Orders.Add(order);
            seed.OrderItems.AddRange(firstSupportItem, secondSupportItem, manualItem);
            seed.Tickets.Add(customerTicket);
            seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        await using var verify = _fixture.CreateDbContext();
        var tickets = await verify.Tickets.Where(x => x.OrderId == order.Id).ToListAsync();
        tickets.Should().HaveCount(2, "a customer ticket never suppresses the automatic fulfilment ticket");
        var fulfillment = tickets.Single(x => x.IsFulfillmentTicket);
        // V0031 assigns the sequential order number at payment time, so the subject carries the
        // persisted post-payment number, not the provisional one this test seeded.
        var paidOrderNumber = await verify.Orders.Where(x => x.Id == order.Id).Select(x => x.OrderNumber).SingleAsync();
        fulfillment.Subject.Should().Contain(paidOrderNumber);
        var unchangedCustomerTicket = tickets.Single(x => x.Id == customerTicket.Id);
        unchangedCustomerTicket.IsFulfillmentTicket.Should().BeFalse();
        unchangedCustomerTicket.Subject.Should().Be(customerTicket.Subject);
        unchangedCustomerTicket.Status.Should().Be(customerTicket.Status);

        var items = await verify.OrderItems.Where(x => x.OrderId == order.Id).ToListAsync();
        items.Single(x => x.Id == firstSupportItem.Id).SupportTicketId.Should().Be(fulfillment.Id);
        items.Single(x => x.Id == secondSupportItem.Id).SupportTicketId.Should().Be(fulfillment.Id);
        items.Single(x => x.Id == manualItem.Id).SupportTicketId.Should().BeNull("manual delivery retains its manual workflow");
        var firstMessage = await verify.TicketMessages.SingleAsync(x => x.TicketId == fulfillment.Id);
        firstMessage.Message.Should().Contain(firstSupportItem.ProductTitle).And.Contain(secondSupportItem.ProductTitle);
    }

    [Fact]
    public async Task Filtered_unique_index_allows_customer_tickets_but_rejects_a_second_fulfillment_ticket_for_the_same_order()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var order = NewOrder(user.Id);
        var customerTicket = new Ticket
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, Subject = "Customer ticket",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Status = (byte)TicketStatus.WaitingForAdmin, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        var fulfillmentTicket = new Ticket
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, Subject = "Fulfillment ticket",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Status = (byte)TicketStatus.WaitingForAdmin, IsFulfillmentTicket = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Orders.Add(order);
            seed.Tickets.AddRange(customerTicket, fulfillmentTicket);
            await seed.SaveChangesAsync();
        }

        await using var duplicate = _fixture.CreateDbContext();
        duplicate.Tickets.Add(new Ticket
        {
            Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, Subject = "Duplicate fulfilment",
            Department = (byte)TicketDepartment.Orders, Priority = (byte)TicketPriority.Normal,
            Status = (byte)TicketStatus.WaitingForAdmin, IsFulfillmentTicket = true,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        Func<Task> saveDuplicate = () => duplicate.SaveChangesAsync();
        await saveDuplicate.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SupportRequired_without_optin_does_not_auto_create_a_ticket()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = NewCategory();
        var product = NewProduct(category.Id, DeliveryType.SupportRequired);
        product.IsActive = false; // keep this catalog fixture out of the public sitemap
        product.RequiresSupportMessage = false; // default: customer-initiated flow preserved
        var order = NewOrder(user.Id);
        var item = NewOrderItem(order.Id, product, DeliveryType.SupportRequired);
        var authority = $"SUP-{Guid.NewGuid():N}";
        var payment = NewPayment(user.Id, order.Id, authority);
        await using (var seed = _fixture.CreateDbContext())
        {
            seed.Categories.Add(category); seed.Products.Add(product);
            seed.Orders.Add(order); seed.OrderItems.Add(item); seed.Payments.Add(payment);
            await seed.SaveChangesAsync();
        }

        await using (var db = _fixture.CreateDbContext())
            await NewPaymentService(db, new SuccessfulGateway(), new NullWallet())
                .VerifyZarinpalPaymentAsync(authority, "OK");

        await using var verify = _fixture.CreateDbContext();
        (await verify.Tickets.CountAsync(x => x.OrderId == order.Id)).Should().Be(0);
        (await verify.OrderItems.SingleAsync(x => x.Id == item.Id)).SupportTicketId.Should().BeNull();
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

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

    private static AesEncryptionService Crypto() => new(Options.Create(new Vitorize.Application.Common.EncryptionSettings
        { Key = "0123456789abcdef0123456789abcdef" }));

    private static Order NewOrder(Guid userId) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"VT-PAY-{Guid.NewGuid():N}",
        Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
        SubtotalAmount = 100m, FinalAmount = 100m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow
    };
    private static Payment NewPayment(Guid userId, Guid orderId, string authority) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, OrderId = orderId, Amount = 100m,
        Gateway = "Zarinpal", Authority = authority, Status = (byte)PaymentStatus.Pending,
        CurrencyType = (byte)CurrencyType.Toman, RequestedAt = DateTime.UtcNow
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
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, CurrencyType currency, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((true, $"A-{Guid.NewGuid():N}", "https://payment.test"));
        public Task<Vitorize.Application.Models.Payments.ZarinpalVerificationResult> VerifyPaymentAsync(string authority, decimal amount)
        { VerifyCount++; return Task.FromResult(new Vitorize.Application.Models.Payments.ZarinpalVerificationResult(true, 12345L)); }
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult("https://payment.test");
    }
    private sealed class FailingGateway : IZarinpalGatewayService
    {
        public Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, CurrencyType currency, string description, string? mobile = null, string? email = null, string? orderId = null) =>
            Task.FromResult((false, string.Empty, string.Empty));
        public Task<Vitorize.Application.Models.Payments.ZarinpalVerificationResult> VerifyPaymentAsync(string authority, decimal amount) =>
            Task.FromResult(new Vitorize.Application.Models.Payments.ZarinpalVerificationResult(false, 0));
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult(string.Empty);
    }
    private sealed class SlowSuccessfulGateway : IZarinpalGatewayService
    {
        private int _createCount;
        public int CreateCount => _createCount;
        public async Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(decimal amount, CurrencyType currency, string description, string? mobile = null, string? email = null, string? orderId = null)
        {
            Interlocked.Increment(ref _createCount);
            await Task.Delay(250);
            return (true, $"SLOW-{Guid.NewGuid():N}", "https://payment.test");
        }
        public Task<Vitorize.Application.Models.Payments.ZarinpalVerificationResult> VerifyPaymentAsync(string authority, decimal amount) => Task.FromResult(new Vitorize.Application.Models.Payments.ZarinpalVerificationResult(true, 1L));
        public Task<string> BuildPaymentUrlAsync(string authority) => Task.FromResult("https://payment.test");
    }
    private sealed class NullGiftDelivery : IGiftCodeDeliveryService
    {
        public Task DeliverOrderAsync(Guid orderId, Guid? deliveredByUserId = null) => Task.CompletedTask;
        public Task<bool> DeliverSatisfiedOrderItemAsync(Guid orderItemId, Guid? deliveredByUserId = null, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }
    private sealed class NullCoupon : ICouponService
    {
        public Task<ValidateCouponResultDto> ValidateAsync(Guid userId, ValidateCouponRequestDto request) => throw new NotSupportedException();
        public Task MarkCouponAsUsedAsync(Guid userId, Guid orderId, Guid couponId) => Task.CompletedTask;
    }
    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message, bool sendSms = false, Guid? smsCreatedByUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CreateBulkAsync(Guid broadcastId, IReadOnlyCollection<Guid> recipientUserIds, string title, string message, bool sendSms = false, Guid? smsCreatedByUserId = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
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
