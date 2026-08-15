using FluentAssertions;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2EFulfillmentReleaseIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2EFulfillmentReleaseIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Concurrent_instant_release_delivers_only_the_exact_paid_allocations_once()
    {
        var seed = await SeedAsync(DeliveryType.Instant, quantity: 3);
        await Task.WhenAll(ReleaseAsync(seed.Item.Id), ReleaseAsync(seed.Item.Id));
        await ReleaseAsync(seed.Item.Id);

        await using var verify = _fixture.CreateDbContext();
        var deliveries = await verify.OrderItemDeliveries.Where(x => x.OrderItemId == seed.Item.Id).ToListAsync();
        deliveries.Should().HaveCount(3);
        deliveries.Select(x => x.GiftCodeId).Should().BeEquivalentTo(seed.CodeIds);
        (await verify.GiftCodes.Where(x => seed.CodeIds.Contains(x.Id)).ToListAsync())
            .Should().OnlyContain(x => x.Status == (byte)GiftCodeStatus.Delivered && x.OrderItemId == seed.Item.Id);
        (await verify.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verify.Tickets.CountAsync(x => x.OrderId == seed.Order.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Manual_is_not_auto_delivered_and_support_work_is_idempotent()
    {
        var manual = await SeedAsync(DeliveryType.Manual, quantity: 1);
        await ReleaseAsync(manual.Item.Id);
        await using (var verifyManual = _fixture.CreateDbContext())
        {
            (await verifyManual.OrderItemDeliveries.CountAsync(x => x.OrderItemId == manual.Item.Id)).Should().Be(0);
            (await verifyManual.Orders.SingleAsync(x => x.Id == manual.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
        }

        var support = await SeedAsync(DeliveryType.SupportRequired, quantity: 1, requiresSupportMessage: true);
        await Task.WhenAll(ReleaseAsync(support.Item.Id), ReleaseAsync(support.Item.Id));
        await ReleaseAsync(support.Item.Id);
        await using var verifySupport = _fixture.CreateDbContext();
        (await verifySupport.Tickets.CountAsync(x => x.OrderId == support.Order.Id && x.IsFulfillmentTicket)).Should().Be(1);
        (await verifySupport.OrderItemDeliveries.CountAsync(x => x.OrderItemId == support.Item.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Awaiting_and_rejected_items_never_release_paid_allocations()
    {
        var seed = await SeedAsync(DeliveryType.Instant, quantity: 1);
        await using (var update = _fixture.CreateDbContext())
        {
            var state = await update.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Item.Id);
            state.Status = (byte)OrderItemKycStatus.Rejected;
            await update.SaveChangesAsync();
        }
        await ReleaseAsync(seed.Item.Id);
        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Item.Id)).Should().Be(0);
        (await verify.GiftCodes.SingleAsync(x => x.Id == seed.CodeIds.Single())).Status.Should().Be((byte)GiftCodeStatus.Sold);
    }

    [Fact]
    public async Task Failed_release_keeps_satisfaction_and_paid_ownership_then_retry_delivers_and_reveals()
    {
        var seed = await SeedAsync(DeliveryType.Instant, quantity: 1);
        using (var customer = _fixture.CreateClient(seed.Token))
        {
            var beforeRelease = await customer.GetAsync("/api/orders/deliveries");
            (await beforeRelease.Content.ReadAsStringAsync()).Should().NotContain(seed.Secret);
        }

        await using (var corrupt = _fixture.CreateDbContext())
        {
            (await corrupt.GiftCodes.SingleAsync(x => x.Id == seed.CodeIds.Single())).Status = (byte)GiftCodeStatus.Reserved;
            await corrupt.SaveChangesAsync();
        }
        Func<Task> failedRelease = () => ReleaseAsync(seed.Item.Id);
        await failedRelease.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
        await using (var failed = _fixture.CreateDbContext())
        {
            (await failed.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
            (await failed.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
            (await failed.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Item.Id)).Should().Be(0);
        }

        await using (var repaired = _fixture.CreateDbContext())
        {
            (await repaired.GiftCodes.SingleAsync(x => x.Id == seed.CodeIds.Single())).Status = (byte)GiftCodeStatus.Sold;
            await repaired.SaveChangesAsync();
        }
        await ReleaseAsync(seed.Item.Id);
        using var releasedCustomer = _fixture.CreateClient(seed.Token);
        var delivered = await releasedCustomer.GetAsync("/api/orders/deliveries");
        delivered.StatusCode.Should().Be(HttpStatusCode.OK);
        (await delivered.Content.ReadAsStringAsync()).Should().Contain(seed.Secret);
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var otherCustomer = _fixture.CreateClient(otherToken);
        var otherDeliveries = await otherCustomer.GetAsync("/api/orders/deliveries");
        (await otherDeliveries.Content.ReadAsStringAsync()).Should().NotContain(seed.Secret);
    }

    [Fact]
    public async Task Broken_instant_release_does_not_prevent_a_satisfied_support_item_and_fresh_scope_retry_recovers()
    {
        var seed = await SeedAsync(DeliveryType.Instant, quantity: 1);
        Guid supportItemId;
        await using (var setup = _fixture.CreateDbContext())
        {
            var source = await setup.OrderItems.SingleAsync(x => x.Id == seed.Item.Id);
            var now = DateTime.UtcNow;
            var product = new Product { Id = Guid.NewGuid(), CategoryId = (await setup.Products.SingleAsync(x => x.Id == source.ProductId)).CategoryId, Title = "P2E support", Slug = $"p2e-support-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.SupportRequired, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var support = new OrderItem { Id = Guid.NewGuid(), OrderId = source.OrderId, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.SupportRequired, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycEvaluatedAmount = 100m, KycPolicyVersionId = source.KycPolicyVersionId, CreatedAt = now };
            setup.AddRange(product, support, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = support.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now });
            (await setup.GiftCodes.SingleAsync(x => x.Id == seed.CodeIds.Single())).Status = (byte)GiftCodeStatus.Reserved;
            await setup.SaveChangesAsync();
            supportItemId = support.Id;
        }
        Func<Task> broken = () => ReleaseAsync(seed.Item.Id);
        await broken.Should().ThrowAsync<Vitorize.Shared.Exceptions.BusinessException>();
        await ReleaseAsync(supportItemId);
        await using (var failed = _fixture.CreateDbContext())
        {
            (await failed.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Item.Id)).Should().Be(0);
            (await failed.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
            (await failed.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
            (await failed.Tickets.CountAsync(x => x.OrderId == seed.Order.Id && x.IsFulfillmentTicket)).Should().Be(1);
        }
        await using (var repair = _fixture.CreateDbContext())
        {
            (await repair.GiftCodes.SingleAsync(x => x.Id == seed.CodeIds.Single())).Status = (byte)GiftCodeStatus.Sold;
            await repair.SaveChangesAsync();
        }
        await ReleaseAsync(seed.Item.Id);
        await using var recovered = _fixture.CreateDbContext();
        (await recovered.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Item.Id)).Should().Be(1);
        (await recovered.Tickets.CountAsync(x => x.OrderId == seed.Order.Id && x.IsFulfillmentTicket)).Should().Be(1);
    }

    private async Task ReleaseAsync(Guid itemId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IOrderItemFulfillmentReleaseService>()
            .ReleaseSatisfiedOrderItemAsync(itemId);
    }

    private async Task<(User User, string Token, string Secret, Order Order, OrderItem Item, List<Guid> CodeIds)> SeedAsync(
        DeliveryType deliveryType, int quantity, bool requiresSupportMessage = false)
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var cryptoScope = _fixture.Factory.Services.CreateScope();
        var crypto = cryptoScope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var now = DateTime.UtcNow;
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p2e-{Guid.NewGuid():N}", Name = "P2E", IsActive = true, CreatedAt = now };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "P2E", CreatedAt = now, PublishedAt = now };
        policy.Versions.Add(version);
        var category = new Category { Id = Guid.NewGuid(), Title = "P2E", Slug = $"p2e-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "P2E", Slug = $"p2e-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)deliveryType, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, RequiresSupportMessage = requiresSupportMessage, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2E-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = quantity * 100m, FinalAmount = quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = quantity, UnitPrice = 100m, TotalPrice = quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)deliveryType, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycEvaluatedAmount = quantity * 100m, KycPolicyVersionId = version.Id, CreatedAt = now };
        await using var db = _fixture.CreateDbContext();
        db.AddRange(policy, category, product, order, item, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now });
        var ids = new List<Guid>();
        var secret = $"P2E-CANARY-{Guid.NewGuid():N}";
        if (deliveryType == DeliveryType.Instant)
        {
            for (var index = 0; index < quantity; index++)
            {
                var code = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id, EncryptedCode = crypto.Encrypt($"{secret}-{index}"), MaskedCode = "****P2E", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Sold, ReservedByUserId = user.Id, ReservedAt = now, SoldAt = now, CreatedAt = now };
                db.Add(code);
                db.GiftCodeReservations.Add(new GiftCodeReservation { Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, OrderItemId = item.Id, ProductId = product.Id, GiftCodeId = code.Id, Status = (byte)GiftCodeReservationStatus.Sold, ReservedAt = now, ExpiresAt = now.AddHours(1), SoldAt = now });
                ids.Add(code.Id);
            }
        }
        await db.SaveChangesAsync();
        return (user, token, secret, order, item, ids);
    }
}
