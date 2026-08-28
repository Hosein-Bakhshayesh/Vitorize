using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2EFulfillmentReleaseHttpIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2EFulfillmentReleaseHttpIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Http_approval_releases_the_exact_paid_code_once_and_reveals_only_to_owner()
    {
        var seed = await SeedPendingProfileAsync((DeliveryType.Instant, 1, true));
        using (var owner = _fixture.CreateClient(seed.UserToken))
        {
            var hidden = await owner.GetAsync("/api/orders/deliveries");
            hidden.StatusCode.Should().Be(HttpStatusCode.OK);
            (await hidden.Content.ReadAsStringAsync()).Should().NotContain(seed.InstantSecret!);
        }
        await using (var before = _fixture.CreateDbContext())
            (await before.Notifications.CountAsync(x => x.UserId == seed.User.Id && x.Type == (byte)NotificationType.GiftCodeDelivered)).Should().Be(0);

        using var admin = _fixture.CreateClient(seed.AdminToken);
        var approved = await admin.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = true });
        approved.StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicate = await admin.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = true });
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var verify = _fixture.CreateDbContext())
        {
            (await verify.UserVerificationProfiles.SingleAsync(x => x.Id == seed.ProfileId)).Status.Should().Be((byte)VerificationStatus.Verified);
            (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Items[0].Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
            (await verify.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
            var deliveries = await verify.OrderItemDeliveries.Where(x => x.OrderItemId == seed.Items[0].Id).ToListAsync();
            deliveries.Should().ContainSingle().Which.GiftCodeId.Should().Be(seed.InstantCodeIds.Single());
            (await verify.Notifications.CountAsync(x => x.UserId == seed.User.Id && x.Type == (byte)NotificationType.GiftCodeDelivered)).Should().Be(1);
        }
        using (var owner = _fixture.CreateClient(seed.UserToken))
            (await (await owner.GetAsync("/api/orders/deliveries")).Content.ReadAsStringAsync()).Should().Contain(seed.InstantSecret!);
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var other = _fixture.CreateClient(otherToken);
        (await (await other.GetAsync("/api/orders/deliveries")).Content.ReadAsStringAsync()).Should().NotContain(seed.InstantSecret!);
    }

    [Fact]
    public async Task Http_approval_releases_mixed_items_independently_and_existing_manual_path_completes_only_after_delivery()
    {
        var seed = await SeedPendingProfileAsync(
            (DeliveryType.Instant, 1, false),
            (DeliveryType.Manual, 1, false),
            (DeliveryType.SupportRequired, 1, false));
        var alreadyDelivered = await AddAlreadyDeliveredNotRequiredInstantAsync(seed.Order, seed.User);
        var instantItem = seed.Items.Single(x => x.DeliveryType == (byte)DeliveryType.Instant);
        var manualItem = seed.Items.Single(x => x.DeliveryType == (byte)DeliveryType.Manual);
        var supportItem = seed.Items.Single(x => x.DeliveryType == (byte)DeliveryType.SupportRequired);
        using var admin = _fixture.CreateClient(seed.AdminToken);
        var blocked = await admin.PostAsJsonAsync($"/api/admin/orders/{seed.Order.Id}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = manualItem.Id, Content = "held" });
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await admin.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = true })).StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var afterApproval = _fixture.CreateDbContext())
        {
            (await afterApproval.OrderItemDeliveries.CountAsync(x => x.OrderItemId == instantItem.Id)).Should().Be(1);
            (await afterApproval.OrderItemDeliveries.CountAsync(x => x.OrderItemId == alreadyDelivered)).Should().Be(1);
            (await afterApproval.OrderItemDeliveries.CountAsync(x => x.OrderItemId == manualItem.Id)).Should().Be(0);
            (await afterApproval.Tickets.CountAsync(x => x.OrderId == seed.Order.Id && x.IsFulfillmentTicket)).Should().Be(1);
            (await afterApproval.Orders.SingleAsync(x => x.Id == seed.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
        }

        (await admin.PostAsJsonAsync($"/api/admin/orders/{seed.Order.Id}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = manualItem.Id, Content = "manual" })).StatusCode.Should().Be(HttpStatusCode.OK);
        var duplicateManual = await admin.PostAsJsonAsync($"/api/admin/orders/{seed.Order.Id}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = manualItem.Id, Content = "manual" });
        duplicateManual.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        // SupportRequired creates its fulfillment ticket when eligible; it is not
        // a Manual delivery and must not acquire delivery or code semantics.
        (await admin.PostAsJsonAsync($"/api/admin/orders/{seed.Order.Id}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = supportItem.Id, Content = "support" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var final = _fixture.CreateDbContext();
        (await final.Orders.SingleAsync(x => x.Id == seed.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
        (await final.OrderItemDeliveries.CountAsync(x => x.OrderItemId == manualItem.Id)).Should().Be(1);
        (await final.OrderItemDeliveries.CountAsync(x => x.OrderItemId == supportItem.Id)).Should().Be(0);
        (await final.Notifications.CountAsync(x => x.UserId == seed.User.Id && x.Type == (byte)NotificationType.ManualDeliveryCompleted)).Should().Be(1);
        // Exactly one GiftCode notification belongs to the independently released Instant item;
        // neither Manual nor SupportRequired may reuse that taxonomy.
        (await final.Notifications.CountAsync(x => x.UserId == seed.User.Id && x.Type == (byte)NotificationType.GiftCodeDelivered)).Should().Be(1);
        (await final.Notifications.CountAsync(x => x.UserId == seed.User.Id && x.Type == (byte)NotificationType.TicketCreated)).Should().Be(1);
    }

    [Fact]
    public async Task Http_approve_vs_reject_race_only_releases_when_approve_wins()
    {
        var seed = await SeedPendingProfileAsync((DeliveryType.Instant, 1, false));
        using var approveClient = _fixture.CreateClient(seed.AdminToken);
        using var rejectClient = _fixture.CreateClient(seed.AdminToken);
        using var gate = new ManualResetEventSlim(false);
        var approve = Task.Run(async () => { gate.Wait(); return await approveClient.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = true }); });
        var reject = Task.Run(async () => { gate.Wait(); return await rejectClient.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = false }); });
        gate.Set();
        var responses = await Task.WhenAll(approve, reject);
        responses.Select(x => x.StatusCode).Should().Contain(HttpStatusCode.OK).And.Contain(HttpStatusCode.Conflict);

        await using var verify = _fixture.CreateDbContext();
        var profile = await verify.UserVerificationProfiles.SingleAsync(x => x.Id == seed.ProfileId);
        var state = await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Items.Single().Id);
        var deliveryCount = await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Items.Single().Id);
        if (profile.Status == (byte)VerificationStatus.Verified)
        {
            state.Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
            deliveryCount.Should().Be(1);
        }
        else
        {
            profile.Status.Should().Be((byte)VerificationStatus.Rejected);
            state.Status.Should().Be((byte)OrderItemKycStatus.Rejected);
            deliveryCount.Should().Be(0);
        }
    }

    [Fact]
    public async Task Http_rejection_keeps_the_held_allocation_owned_and_undelivered()
    {
        var seed = await SeedPendingProfileAsync((DeliveryType.Instant, 1, false));
        using var admin = _fixture.CreateClient(seed.AdminToken);
        (await admin.PostAsJsonAsync($"/api/admin/verifications/{seed.ProfileId}/review", new ReviewVerificationRequestDto { Approve = false })).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verify = _fixture.CreateDbContext();
        (await verify.UserVerificationProfiles.SingleAsync(x => x.Id == seed.ProfileId)).Status.Should().Be((byte)VerificationStatus.Rejected);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.Items.Single().Id)).Status.Should().Be((byte)OrderItemKycStatus.Rejected);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.Items.Single().Id)).Should().Be(0);
        (await verify.GiftCodes.SingleAsync(x => x.Id == seed.InstantCodeIds.Single())).Status.Should().Be((byte)GiftCodeStatus.Sold);
    }

    private async Task<(User User, string UserToken, string AdminToken, Guid ProfileId, Order Order, List<OrderItem> Items, List<Guid> InstantCodeIds, string? InstantSecret)> SeedPendingProfileAsync(params (DeliveryType Type, int Quantity, bool Canary)[] definitions)
    {
        var (user, userToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var now = DateTime.UtcNow;
        var doc = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p2e-http-{Guid.NewGuid():N}", Title = "Identity", IsActive = true, CreatedAt = now };
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p2e-http-{Guid.NewGuid():N}", Name = "P2E", IsActive = true, CreatedAt = now };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "P2E", CreatedAt = now, PublishedAt = now };
        policy.Versions.Add(version);
        var category = new Category { Id = Guid.NewGuid(), Title = "P2E", Slug = $"p2e-http-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2E-HTTP-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = definitions.Sum(x => x.Quantity * 100m), FinalAmount = definitions.Sum(x => x.Quantity * 100m), CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
        var items = new List<OrderItem>(); var codeIds = new List<Guid>(); string? secret = null;
        await using var db = _fixture.CreateDbContext();
        db.AddRange(doc, policy, category, order, new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = version.Id, KycDocumentTypeId = doc.Id, IsRequired = true });
        foreach (var definition in definitions)
        {
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = definition.Type.ToString(), Slug = $"p2e-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)definition.Type, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = definition.Quantity, UnitPrice = 100m, TotalPrice = definition.Quantity * 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)definition.Type, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycEvaluatedAmount = definition.Quantity * 100m, KycPolicyVersionId = version.Id, CreatedAt = now };
            db.AddRange(product, item, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now });
            items.Add(item);
            if (definition.Type == DeliveryType.Instant)
                for (var i = 0; i < definition.Quantity; i++)
                {
                    secret ??= $"P2E-HTTP-CANARY-{Guid.NewGuid():N}";
                    var code = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id, EncryptedCode = crypto.Encrypt($"{secret}-{i}"), MaskedCode = "****P2E", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Sold, ReservedByUserId = user.Id, ReservedAt = now, SoldAt = now, CreatedAt = now };
                    db.AddRange(code, new GiftCodeReservation { Id = Guid.NewGuid(), UserId = user.Id, OrderId = order.Id, OrderItemId = item.Id, ProductId = product.Id, GiftCodeId = code.Id, Status = (byte)GiftCodeReservationStatus.Sold, ReservedAt = now, ExpiresAt = now.AddHours(1), SoldAt = now });
                    codeIds.Add(code.Id);
                }
        }
        await db.SaveChangesAsync();
        var verification = scope.ServiceProvider.GetRequiredService<IVerificationService>();
        var profile = await verification.SubmitAsync(user.Id, new SubmitVerificationRequestDto { FirstName = "Test", LastName = "User", NationalCode = "1234567890", RegisteredMobileBelongsToCardHolder = true });
        await verification.AddDocumentAsync(user.Id, 1, $"kyc-private:{user.Id:N}/identity.jpg", doc.Id, items[0].Id);
        await verification.SubmitAsync(user.Id, new SubmitVerificationRequestDto { FirstName = "Test", LastName = "User", NationalCode = "1234567890", RegisteredMobileBelongsToCardHolder = true });
        return (user, userToken, adminToken, profile.Id, order, items, codeIds, secret);
    }

    private async Task<Guid> AddAlreadyDeliveredNotRequiredInstantAsync(Order order, User user)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var now = DateTime.UtcNow;
        await using var db = _fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Title = "P2E normal", Slug = $"p2e-normal-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "P2E normal", Slug = $"p2e-normal-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Instant, DeliveryStatus = (byte)DeliveryStatus.Delivered, DeliveredAt = now, RequiresVerification = false, KycRequirementMode = (byte)KycRequirementMode.None, CreatedAt = now };
        var code = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = item.Id, EncryptedCode = crypto.Encrypt("P2E-NORMAL"), MaskedCode = "****N", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Delivered, ReservedByUserId = user.Id, SoldAt = now, DeliveredAt = now, CreatedAt = now };
        db.AddRange(category, product, item, code,
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.NotRequired, CreatedAt = now, UpdatedAt = now },
            new OrderItemDelivery { Id = Guid.NewGuid(), OrderItemId = item.Id, DeliveryType = (byte)DeliveryType.Instant, GiftCodeId = code.Id, DeliveredContent = crypto.Encrypt("P2E-NORMAL"), ContentHash = new string('A', 64), EncryptionVersion = 2, IsVisibleToCustomer = true, CreatedAt = now });
        await db.SaveChangesAsync();
        return item.Id;
    }
}
