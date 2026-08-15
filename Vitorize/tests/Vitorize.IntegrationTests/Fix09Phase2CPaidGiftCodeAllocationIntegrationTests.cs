using System.Net;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.GiftCodes;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2CPaidGiftCodeAllocationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2CPaidGiftCodeAllocationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Held_qty_three_is_durably_allocated_once_never_revealed_and_never_resold()
    {
        var seeded = await SeedOrderAsync(paid: true, requiresKyc: true, quantity: 3, reservedCount: 3);
        await Task.WhenAll(ProcessAsync(seeded.Order.Id), ProcessAsync(seeded.Order.Id));
        await ProcessAsync(seeded.Order.Id);

        await using (var verify = _fixture.CreateDbContext())
        {
            (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seeded.Item.Id)).Status
                .Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
            var reservations = await verify.GiftCodeReservations.Where(x => x.OrderItemId == seeded.Item.Id).ToListAsync();
            reservations.Should().HaveCount(3).And.OnlyContain(x => x.Status == (byte)GiftCodeReservationStatus.Sold);
            var codes = await verify.GiftCodes.Where(x => seeded.GiftCodeIds.Contains(x.Id)).ToListAsync();
            codes.Should().HaveCount(3).And.OnlyContain(x => x.Status == (byte)GiftCodeStatus.Sold && x.OrderItemId == seeded.Item.Id);
            (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.Item.Id)).Should().Be(0);
            (await verify.Orders.SingleAsync(x => x.Id == seeded.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
            (await verify.Notifications.CountAsync(x => x.UserId == seeded.User.Id && x.Type == (byte)NotificationType.GiftCodeDelivered)).Should().Be(0);
            foreach (var reservation in reservations) reservation.ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await verify.SaveChangesAsync();
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGiftCodeReservationService>().ReleaseExpiredReservationsAsync();

        await using (var afterCleanup = _fixture.CreateDbContext())
            (await afterCleanup.GiftCodes.Where(x => seeded.GiftCodeIds.Contains(x.Id)).ToListAsync())
                .Should().OnlyContain(x => x.Status == (byte)GiftCodeStatus.Sold);

        var (otherUser, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var reserveDb = _fixture.CreateDbContext())
        {
            var reservations = new GiftCodeReservationService(reserveDb);
            Func<Task> reserve = () => reservations.ReserveAsync(otherUser.Id, new ReserveGiftCodeRequestDto { ProductId = seeded.Product.Id });
            await reserve.Should().ThrowAsync<BusinessException>();
        }

        using var client = _fixture.CreateClient(seeded.Token);
        var details = await client.GetAsync($"/api/orders/{seeded.Order.Id}");
        var deliveries = await client.GetAsync("/api/orders/deliveries");
        details.StatusCode.Should().Be(HttpStatusCode.OK);
        deliveries.StatusCode.Should().Be(HttpStatusCode.OK);
        (await details.Content.ReadAsStringAsync()).Should().NotContain(seeded.Secret);
        (await deliveries.Content.ReadAsStringAsync()).Should().NotContain(seeded.Secret);
    }

    [Fact]
    public async Task Mixed_eligible_and_held_items_fulfill_independently()
    {
        var held = await SeedOrderAsync(paid: true, requiresKyc: true, quantity: 2, reservedCount: 2);
        var eligible = await AddInstantItemAsync(held.Order, held.User, requiresKyc: false, quantity: 1, codePrefix: "MIX-ELIGIBLE");
        await ProcessAsync(held.Order.Id);

        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == eligible.Item.Id)).Should().Be(1);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == held.Item.Id)).Should().Be(0);
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderItemId == held.Item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(2);
        (await verify.Orders.SingleAsync(x => x.Id == held.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
    }

    [Fact]
    public async Task Allocation_failure_keeps_payment_paid_and_recovers_without_partial_delivery()
    {
        var seeded = await SeedOrderAsync(paid: true, requiresKyc: true, quantity: 2, reservedCount: 1);
        Func<Task> allocation = () => ProcessAsync(seeded.Order.Id);
        await allocation.Should().ThrowAsync<BusinessException>();
        await using (var failed = _fixture.CreateDbContext())
        {
            (await failed.Orders.SingleAsync(x => x.Id == seeded.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
            (await failed.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.Item.Id)).Should().Be(0);
            (await failed.GiftCodeReservations.CountAsync(x => x.OrderItemId == seeded.Item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(0);
        }

        await AddReservedCodeAsync(seeded.Order, seeded.User, seeded.Item, seeded.Product, "RECOVERY");
        await ProcessAsync(seeded.Order.Id);
        await using var recovered = _fixture.CreateDbContext();
        (await recovered.GiftCodeReservations.CountAsync(x => x.OrderItemId == seeded.Item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(2);
        (await recovered.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.Item.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Temporary_unpaid_reservations_expire_while_rejected_and_final_rejected_allocations_remain_owned()
    {
        var unpaid = await SeedOrderAsync(paid: false, requiresKyc: false, quantity: 1, reservedCount: 1);
        await using (var expire = _fixture.CreateDbContext())
        {
            (await expire.GiftCodeReservations.SingleAsync(x => x.OrderItemId == unpaid.Item.Id)).ExpiresAt = DateTime.UtcNow.AddMinutes(-5);
            await expire.SaveChangesAsync();
        }
        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IGiftCodeReservationService>().ReleaseExpiredReservationsAsync();
        await using (var released = _fixture.CreateDbContext())
        {
            (await released.GiftCodeReservations.SingleAsync(x => x.OrderItemId == unpaid.Item.Id)).Status.Should().Be((byte)GiftCodeReservationStatus.Expired);
            (await released.GiftCodes.SingleAsync(x => x.OrderItemId == unpaid.Item.Id)).Status.Should().Be((byte)GiftCodeStatus.Available);
        }

        var paid = await SeedOrderAsync(paid: true, requiresKyc: true, quantity: 1, reservedCount: 1);
        await ProcessAsync(paid.Order.Id);
        await using (var stateDb = _fixture.CreateDbContext())
        {
            var state = await stateDb.OrderItemKycStates.SingleAsync(x => x.OrderItemId == paid.Item.Id);
            state.Status = (byte)OrderItemKycStatus.Rejected;
            await stateDb.SaveChangesAsync();
            state.Status = (byte)OrderItemKycStatus.FinalRejected;
            await stateDb.SaveChangesAsync();
        }
        await using var retained = _fixture.CreateDbContext();
        (await retained.GiftCodeReservations.SingleAsync(x => x.OrderItemId == paid.Item.Id)).Status.Should().Be((byte)GiftCodeReservationStatus.Sold);
        (await retained.GiftCodes.SingleAsync(x => x.OrderItemId == paid.Item.Id)).Status.Should().Be((byte)GiftCodeStatus.Sold);
        (await retained.OrderItemDeliveries.CountAsync(x => x.OrderItemId == paid.Item.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Wallet_payment_uses_the_same_held_allocation_path_once()
    {
        var seeded = await SeedOrderAsync(paid: false, requiresKyc: true, quantity: 1, reservedCount: 1);
        await using (var setup = _fixture.CreateDbContext())
        {
            setup.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = seeded.User.Id, Balance = 1_000m, CreatedAt = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var payment = await scope.ServiceProvider.GetRequiredService<IPaymentService>()
                .PayWithWalletAsync(seeded.User.Id, seeded.Order.Id);
            payment.IsPaid.Should().BeTrue();
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.Payments.CountAsync(x => x.OrderId == seeded.Order.Id && x.Gateway == "Wallet" && x.Status == (byte)PaymentStatus.Paid)).Should().Be(1);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == seeded.User.Id && x.Type == (byte)WalletTransactionType.Debit)).Should().Be(1);
        (await verify.GiftCodeReservations.SingleAsync(x => x.OrderItemId == seeded.Item.Id)).Status.Should().Be((byte)GiftCodeReservationStatus.Sold);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seeded.Item.Id)).Should().Be(0);
    }

    private async Task ProcessAsync(Guid orderId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPostPaymentOrderProcessor>().ProcessPaidOrderAsync(orderId);
    }

    private async Task<(Order Order, OrderItem Item, Product Product, User User, string Token, List<Guid> GiftCodeIds, string Secret)> SeedOrderAsync(
        bool paid, bool requiresKyc, int quantity, int reservedCount)
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var now = DateTime.UtcNow;
        await using var db = _fixture.CreateDbContext();
        var storedUser = await db.Users.SingleAsync(x => x.Id == user.Id);
        storedUser.IsMobileConfirmed = !requiresKyc;
        storedUser.VerificationStatus = requiresKyc ? (byte)VerificationStatus.Pending : (byte)VerificationStatus.Verified;
        var category = new Category { Id = Guid.NewGuid(), Title = "Phase2C", Slug = $"p2c-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        Guid? policyVersionId = null;
        if (requiresKyc)
        {
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p2c-{Guid.NewGuid():N}", Name = "Phase 2C", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Phase 2C", CreatedAt = now, PublishedAt = now };
            policy.Versions.Add(version);
            db.KycPolicies.Add(policy);
            policyVersionId = version.Id;
        }
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Phase2C Instant", Slug = $"p2c-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2C-{Guid.NewGuid():N}", Status = paid ? (byte)OrderStatus.Processing : (byte)OrderStatus.PendingPayment, PaymentStatus = paid ? (byte)PaymentStatus.Paid : (byte)PaymentStatus.Pending, SubtotalAmount = quantity * 100m, FinalAmount = quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = paid ? now : null };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = quantity, UnitPrice = 100m, TotalPrice = quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Instant, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = requiresKyc, KycRequirementMode = requiresKyc ? (byte)KycRequirementMode.Always : (byte)KycRequirementMode.None, KycEvaluatedAmount = quantity * 100m, KycPolicyVersionId = policyVersionId, CreatedAt = now };
        db.AddRange(category, product, order, item);
        var secret = $"FIX09-SECRET-{Guid.NewGuid():N}";
        var ids = new List<Guid>();
        for (var index = 0; index < reservedCount; index++)
            ids.Add(await AddReservedCodeAsync(db, order, user, item, product, Encrypt($"{secret}-{index}"), now));
        await db.SaveChangesAsync();
        return (order, item, product, user, token, ids, secret);
    }

    private async Task<(OrderItem Item, List<Guid> GiftCodeIds)> AddInstantItemAsync(Order order, User user, bool requiresKyc, int quantity, string codePrefix)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Title = codePrefix, Slug = $"{codePrefix.ToLowerInvariant()}-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = codePrefix, Slug = $"{codePrefix.ToLowerInvariant()}-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = quantity, UnitPrice = 100m, TotalPrice = quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Instant, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = requiresKyc, KycRequirementMode = requiresKyc ? (byte)KycRequirementMode.Always : (byte)KycRequirementMode.None, CreatedAt = DateTime.UtcNow };
        db.AddRange(category, product, item);
        var ids = new List<Guid>();
        for (var index = 0; index < quantity; index++) ids.Add(await AddReservedCodeAsync(db, order, user, item, product, Encrypt($"{codePrefix}-{index}"), DateTime.UtcNow));
        await db.SaveChangesAsync();
        return (item, ids);
    }

    private async Task AddReservedCodeAsync(Order order, User user, OrderItem item, Product product, string prefix)
    {
        await using var db = _fixture.CreateDbContext();
        await AddReservedCodeAsync(db, order, user, item, product, Encrypt(prefix), DateTime.UtcNow);
        await db.SaveChangesAsync();
    }

    private static Task<Guid> AddReservedCodeAsync(Vitorize.Infrastructure.Persistence.VitorizeDbContext db, Order order, User user, OrderItem item, Product product, string encryptedCode, DateTime now)
    {
        var code = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id, EncryptedCode = encryptedCode, MaskedCode = "****P2C", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Reserved, ReservedByUserId = user.Id, ReservedAt = now, ReservationExpiresAt = now.AddMinutes(10), CreatedAt = now };
        var reservation = new GiftCodeReservation { Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, OrderItemId = item.Id, ProductId = product.Id, GiftCodeId = code.Id, Status = (byte)GiftCodeReservationStatus.Active, ReservedAt = now, ExpiresAt = now.AddMinutes(10) };
        db.AddRange(code, reservation);
        return Task.FromResult(code.Id);
    }

    private string Encrypt(string plaintext)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IEncryptionService>().Encrypt(plaintext);
    }
}
