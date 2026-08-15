using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2BPostPaymentKycIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2BPostPaymentKycIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Paid_items_initialize_from_snapshot_fulfill_once_and_are_audited()
    {
        var seeded = await SeedPaidOrderAsync(verified: true, (false, DeliveryType.Instant, false), (true, DeliveryType.Instant, false));
        await ProcessAsync(seeded.Order.Id);
        await ProcessAsync(seeded.Order.Id);

        await using var verify = _fixture.CreateDbContext();
        var states = await verify.OrderItemKycStates.Where(x => seeded.ItemIds.Contains(x.OrderItemId)).ToListAsync();
        states.Should().ContainSingle(x => x.OrderItemId == seeded.ItemIds[0] && x.Status == (byte)OrderItemKycStatus.NotRequired);
        states.Should().ContainSingle(x => x.OrderItemId == seeded.ItemIds[1] && x.Status == (byte)OrderItemKycStatus.Satisfied);
        (await verify.OrderItemDeliveries.CountAsync(x => seeded.ItemIds.Contains(x.OrderItemId))).Should().Be(2);
        (await verify.Orders.SingleAsync(x => x.Id == seeded.Order.Id)).Status.Should().Be((byte)OrderStatus.Completed);
        (await verify.AuditLogs.CountAsync(x => x.EntityName == nameof(OrderItemKycState) && x.ActionType == "Create"))
            .Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Held_and_mixed_items_do_not_block_eligible_items_or_reset_existing_state()
    {
        var seeded = await SeedPaidOrderAsync(verified: false, (false, DeliveryType.Instant, false), (true, DeliveryType.Instant, false), (true, DeliveryType.Instant, false));
        var now = DateTime.UtcNow;
        await using (var setup = _fixture.CreateDbContext())
        {
            setup.OrderItemKycStates.AddRange(
                State(seeded.ItemIds[1], OrderItemKycStatus.Satisfied, now),
                State(seeded.ItemIds[2], OrderItemKycStatus.AwaitingSubmission, now));
            await setup.SaveChangesAsync();
        }

        await ProcessAsync(seeded.Order.Id);
        await ProcessAsync(seeded.Order.Id);

        await using var verify = _fixture.CreateDbContext();
        var states = await verify.OrderItemKycStates.Where(x => seeded.ItemIds.Contains(x.OrderItemId)).ToListAsync();
        states.Should().HaveCount(3);
        states.Single(x => x.OrderItemId == seeded.ItemIds[0]).Status.Should().Be((byte)OrderItemKycStatus.NotRequired);
        states.Single(x => x.OrderItemId == seeded.ItemIds[1]).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
        states.Single(x => x.OrderItemId == seeded.ItemIds[2]).Status.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.ItemIds[0] || x.OrderItemId == seeded.ItemIds[1])).Should().Be(2);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.ItemIds[2])).Should().Be(0);
        (await verify.GiftCodes.SingleAsync(x => x.OrderItemId == seeded.ItemIds[2])).Status.Should().Be((byte)GiftCodeStatus.Sold);
        (await verify.Orders.SingleAsync(x => x.Id == seeded.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);

        await using (var transition = _fixture.CreateDbContext())
        {
            var held = await transition.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seeded.ItemIds[2]);
            held.Status = (byte)OrderItemKycStatus.AwaitingReview;
            held.UpdatedAt = DateTime.UtcNow;
            await transition.SaveChangesAsync();
        }
        await ProcessAsync(seeded.Order.Id);
        await using var unchanged = _fixture.CreateDbContext();
        (await unchanged.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seeded.ItemIds[2])).Status
            .Should().Be((byte)OrderItemKycStatus.AwaitingReview);
    }

    [Fact]
    public async Task Concurrent_processing_is_idempotent_and_unpaid_orders_are_ignored()
    {
        var paid = await SeedPaidOrderAsync(verified: false, (false, DeliveryType.Instant, false));
        await Task.WhenAll(ProcessAsync(paid.Order.Id), ProcessAsync(paid.Order.Id));
        await using (var verify = _fixture.CreateDbContext())
        {
            (await verify.OrderItemKycStates.CountAsync(x => x.OrderItemId == paid.ItemIds[0])).Should().Be(1);
            (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == paid.ItemIds[0])).Should().Be(1);
        }

        var unpaid = await SeedUnpaidOrderAsync(verified: false, (false, DeliveryType.Instant, false));
        await ProcessAsync(unpaid.Order.Id);
        await using var check = _fixture.CreateDbContext();
        (await check.OrderItemKycStates.CountAsync(x => x.OrderItemId == unpaid.ItemIds[0])).Should().Be(0);
        (await check.OrderItemDeliveries.CountAsync(x => x.OrderItemId == unpaid.ItemIds[0])).Should().Be(0);
    }

    [Fact]
    public async Task Manual_and_support_backend_paths_refuse_held_items_but_allow_satisfied_items()
    {
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var manual = await SeedPaidOrderAsync(verified: false, (true, DeliveryType.Manual, false));
        await using (var setup = _fixture.CreateDbContext())
        {
            setup.OrderItemKycStates.Add(State(manual.ItemIds[0], OrderItemKycStatus.AwaitingSubmission, DateTime.UtcNow));
            await setup.SaveChangesAsync();
        }
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        await using (var blockedDb = _fixture.CreateDbContext())
        {
            var service = new OrderService(blockedDb, new TestNotifications(), crypto);
            Func<Task> action = () => service.DeliverManualAsync(manual.Order.Id, admin.Id,
                new ManualDeliveryRequestDto { OrderItemId = manual.ItemIds[0], Content = "held" });
            await action.Should().ThrowAsync<BusinessException>();
        }
        await using (var release = _fixture.CreateDbContext())
        {
            var state = await release.OrderItemKycStates.SingleAsync(x => x.OrderItemId == manual.ItemIds[0]);
            state.Status = (byte)OrderItemKycStatus.Satisfied; state.UpdatedAt = DateTime.UtcNow;
            await release.SaveChangesAsync();
        }
        await using (var deliver = _fixture.CreateDbContext())
            await new OrderService(deliver, new TestNotifications(), crypto).DeliverManualAsync(manual.Order.Id, admin.Id,
                new ManualDeliveryRequestDto { OrderItemId = manual.ItemIds[0], Content = "released" });

        var support = await SeedPaidOrderAsync(verified: false, (true, DeliveryType.SupportRequired, true));
        await using (var setup = _fixture.CreateDbContext())
        {
            setup.OrderItemKycStates.Add(State(support.ItemIds[0], OrderItemKycStatus.AwaitingSubmission, DateTime.UtcNow));
            await setup.SaveChangesAsync();
        }
        await ProcessAsync(support.Order.Id);
        await using (var held = _fixture.CreateDbContext())
            (await held.Tickets.CountAsync(x => x.OrderId == support.Order.Id && x.IsFulfillmentTicket)).Should().Be(0);
        await using (var release = _fixture.CreateDbContext())
        {
            var state = await release.OrderItemKycStates.SingleAsync(x => x.OrderItemId == support.ItemIds[0]);
            state.Status = (byte)OrderItemKycStatus.Satisfied; state.UpdatedAt = DateTime.UtcNow;
            await release.SaveChangesAsync();
        }
        await ProcessAsync(support.Order.Id); await ProcessAsync(support.Order.Id);
        await using var supported = _fixture.CreateDbContext();
        (await supported.Tickets.CountAsync(x => x.OrderId == support.Order.Id && x.IsFulfillmentTicket)).Should().Be(1);
    }

    private async Task ProcessAsync(Guid orderId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPostPaymentOrderProcessor>().ProcessPaidOrderAsync(orderId);
    }

    private Task<(Order Order, List<Guid> ItemIds)> SeedPaidOrderAsync(bool verified,
        params (bool RequiresKyc, DeliveryType Delivery, bool SupportOptIn)[] items) =>
        SeedOrderAsync(verified, paid: true, items);

    private Task<(Order Order, List<Guid> ItemIds)> SeedUnpaidOrderAsync(bool verified,
        params (bool RequiresKyc, DeliveryType Delivery, bool SupportOptIn)[] items) =>
        SeedOrderAsync(verified, paid: false, items);

    private async Task<(Order Order, List<Guid> ItemIds)> SeedOrderAsync(bool verified, bool paid,
        params (bool RequiresKyc, DeliveryType Delivery, bool SupportOptIn)[] items)
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var cryptoScope = _fixture.Factory.Services.CreateScope();
        var crypto = cryptoScope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var now = DateTime.UtcNow;
        await using var db = _fixture.CreateDbContext();
        var policyVersionId = await db.KycPolicyVersions.Where(x => x.KycPolicy.Code == "legacy-profile-verification").Select(x => x.Id).SingleAsync();
        var storedUser = await db.Users.SingleAsync(x => x.Id == user.Id);
        storedUser.IsMobileConfirmed = verified;
        storedUser.VerificationStatus = verified ? (byte)VerificationStatus.Verified : (byte)VerificationStatus.Pending;
        var category = new Category { Id = Guid.NewGuid(), Title = "Phase2B", Slug = $"phase2b-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2B-{Guid.NewGuid():N}",
            Status = paid ? (byte)OrderStatus.Processing : (byte)OrderStatus.PendingPayment,
            PaymentStatus = paid ? (byte)PaymentStatus.Paid : (byte)PaymentStatus.Pending,
            SubtotalAmount = items.Length * 100m, DiscountAmount = 0m, FinalAmount = items.Length * 100m,
            CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = paid ? now : null };
        db.AddRange(category, order);
        var itemIds = new List<Guid>();
        for (var index = 0; index < items.Length; index++)
        {
            var definition = items[index];
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = $"Phase2B {index}",
                Slug = $"phase2b-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)definition.Delivery,
                BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true,
                RequiresSupportMessage = definition.SupportOptIn, CreatedAt = now };
            var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
                Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = (byte)CurrencyType.Toman,
                DeliveryType = (byte)definition.Delivery, DeliveryStatus = (byte)DeliveryStatus.Pending,
                RequiresVerification = definition.RequiresKyc,
                KycRequirementMode = definition.RequiresKyc ? (byte)KycRequirementMode.Always : (byte)KycRequirementMode.None,
                KycThresholdAmount = null, KycEvaluatedAmount = definition.RequiresKyc ? 100m : 0m,
                KycPolicyVersionId = definition.RequiresKyc ? policyVersionId : null, CreatedAt = now };
            db.AddRange(product, item); itemIds.Add(item.Id);
            if (definition.Delivery == DeliveryType.Instant && paid)
            {
                var secret = $"P2B-{Guid.NewGuid():N}";
                var gift = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id,
                    EncryptedCode = crypto.Encrypt(secret), MaskedCode = "****P2B",
                    CodeHashFingerprint = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret))),
                    EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Sold, ReservedByUserId = user.Id,
                    ReservedAt = now, SoldAt = now, CreatedAt = now };
                db.Add(gift);
                db.GiftCodeReservations.Add(new GiftCodeReservation { Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id,
                    OrderItemId = item.Id, ProductId = product.Id, GiftCodeId = gift.Id,
                    Status = (byte)GiftCodeReservationStatus.Sold, ReservedAt = now, ExpiresAt = now.AddHours(1), SoldAt = now });
            }
        }
        await db.SaveChangesAsync();
        return (order, itemIds);
    }

    private static OrderItemKycState State(Guid itemId, OrderItemKycStatus status, DateTime now) => new()
    { Id = Guid.NewGuid(), OrderItemId = itemId, Status = (byte)status, CreatedAt = now, UpdatedAt = now };

    private sealed class TestNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message) => Task.CompletedTask;
        public Task<List<Vitorize.Application.DTOs.Notifications.NotificationDto>> GetMyNotificationsAsync(Guid userId) => Task.FromResult(new List<Vitorize.Application.DTOs.Notifications.NotificationDto>());
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userId, Guid notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
    }
}
