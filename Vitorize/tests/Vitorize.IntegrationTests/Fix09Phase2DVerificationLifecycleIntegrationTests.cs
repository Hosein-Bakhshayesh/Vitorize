using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2DVerificationLifecycleIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2DVerificationLifecycleIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Required_documents_across_a_pending_profile_move_items_to_review_without_a_second_submit()
    {
        var seed = await SeedAsync();
        Guid profileId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            profileId = (await service.SubmitAsync(seed.User.Id, Request())).Id;
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/a.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/b.jpg", seed.DocumentB.Id, seed.V2Item.Id);
        }

        await using (var afterSubmission = _fixture.CreateDbContext())
        {
            (await afterSubmission.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V1Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingReview);
            (await afterSubmission.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V2Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingReview);
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IVerificationService>()
                .ReviewAsync(profileId, seed.Admin.Id, new ReviewVerificationRequestDto { Approve = true });

        await using var approved = _fixture.CreateDbContext();
        var v1 = await approved.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V1Item.Id);
        v1.Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
        v1.SatisfiedByVerificationProfileId.Should().Be(profileId);
        (await approved.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V2Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
        (await approved.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id)).Should().Be(0);
        (await approved.Orders.SingleAsync(x => x.Id == seed.Order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
    }

    [Fact]
    public async Task Rejected_item_resubmits_but_final_rejected_item_never_reopens()
    {
        var seed = await SeedAsync();
        Guid profileId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            profileId = (await service.SubmitAsync(seed.User.Id, Request())).Id;
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/a1.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/b1.jpg", seed.DocumentB.Id, seed.V2Item.Id);
            await service.SubmitAsync(seed.User.Id, Request());
            await service.ReviewAsync(profileId, seed.Admin.Id, new ReviewVerificationRequestDto { Approve = false });
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/a2.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/b2.jpg", seed.DocumentB.Id, seed.V2Item.Id);
            await service.SubmitAsync(seed.User.Id, Request());
        }
        await using (var resubmitted = _fixture.CreateDbContext())
        {
            (await resubmitted.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V2Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingReview);
            var terminal = await resubmitted.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V1Item.Id);
            terminal.Status = (byte)OrderItemKycStatus.FinalRejected;
            await resubmitted.SaveChangesAsync();
        }
        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IVerificationService>().SubmitAsync(seed.User.Id, Request());
        await using var final = _fixture.CreateDbContext();
        (await final.OrderItemKycStates.SingleAsync(x => x.OrderItemId == seed.V1Item.Id)).Status.Should().Be((byte)OrderItemKycStatus.FinalRejected);
        (await final.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Http_concurrent_approve_and_reject_leave_the_profile_and_all_items_consistent()
    {
        var seed = await SeedAsync();
        Guid profileId;
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            profileId = (await service.SubmitAsync(seed.User.Id, Request())).Id;
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/con-a.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/con-b.jpg", seed.DocumentB.Id, seed.V2Item.Id);
            await service.SubmitAsync(seed.User.Id, Request());
        }

        int profileAuditsBeforeRace;
        await using (var beforeRace = _fixture.CreateDbContext())
            profileAuditsBeforeRace = await beforeRace.AuditLogs.CountAsync(x =>
                x.EntityName == nameof(UserVerificationProfile) && x.EntityId == profileId.ToString());

        // Independent real API requests begin together; SQL applock decides the winner.
        using var approveClient = _fixture.CreateClient(seed.AdminToken);
        using var rejectClient = _fixture.CreateClient(seed.AdminToken);
        var responses = await PostConcurrentlyAsync(
            approveClient, rejectClient,
            $"/api/admin/verifications/{profileId}/review",
            new ReviewVerificationRequestDto { Approve = true },
            new ReviewVerificationRequestDto { Approve = false });
        responses.Select(x => x.StatusCode).Should().Contain(HttpStatusCode.OK).And.Contain(HttpStatusCode.Conflict);

        await using var verify = _fixture.CreateDbContext();
        var profile = await verify.UserVerificationProfiles.SingleAsync(x => x.Id == profileId);
        var states = await verify.OrderItemKycStates.Where(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id).ToListAsync();
        if (profile.Status == (byte)VerificationStatus.Verified)
            states.Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.Satisfied);
        else
        {
            profile.Status.Should().Be((byte)VerificationStatus.Rejected);
            states.Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.Rejected);
        }
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id)).Should().Be(0);
        (await verify.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verify.AuditLogs.CountAsync(x =>
            x.EntityName == nameof(UserVerificationProfile) && x.EntityId == profileId.ToString()))
            .Should().Be(profileAuditsBeforeRace + 1, "only the committed winning review is audited");
    }

    [Fact]
    public async Task Concurrent_duplicate_customer_submits_are_idempotent()
    {
        var seed = await SeedAsync();
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            await service.SubmitAsync(seed.User.Id, Request());
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/submit-a.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/submit-b.jpg", seed.DocumentB.Id, seed.V2Item.Id);
        }
        using var first = _fixture.CreateClient(seed.UserToken);
        using var second = _fixture.CreateClient(seed.UserToken);
        var requests = await PostConcurrentlyAsync(first, second, "/api/verification/submit", Request(), Request());
        requests.Should().OnlyContain(x => x.IsSuccessStatusCode);
        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemKycStates.Where(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id).ToListAsync())
            .Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.AwaitingReview);
        (await verify.UserVerificationProfiles.CountAsync(x => x.UserId == seed.User.Id)).Should().Be(1);
    }

    [Fact]
    public async Task Concurrent_duplicate_approve_and_reject_are_idempotent()
    {
        var approveSeed = await SeedAsync();
        var approveProfileId = await PrepareForReviewAsync(approveSeed);
        using (var first = _fixture.CreateClient(approveSeed.AdminToken))
        using (var second = _fixture.CreateClient(approveSeed.AdminToken))
        {
            var responses = await PostConcurrentlyAsync(
                first, second, $"/api/admin/verifications/{approveProfileId}/review",
                new ReviewVerificationRequestDto { Approve = true }, new ReviewVerificationRequestDto { Approve = true });
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
        }
        await using (var approved = _fixture.CreateDbContext())
            (await approved.OrderItemKycStates.Where(x => x.OrderItemId == approveSeed.V1Item.Id || x.OrderItemId == approveSeed.V2Item.Id).ToListAsync())
                .Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.Satisfied);

        var rejectSeed = await SeedAsync();
        var rejectProfileId = await PrepareForReviewAsync(rejectSeed);
        using (var first = _fixture.CreateClient(rejectSeed.AdminToken))
        using (var second = _fixture.CreateClient(rejectSeed.AdminToken))
        {
            var responses = await PostConcurrentlyAsync(
                first, second, $"/api/admin/verifications/{rejectProfileId}/review",
                new ReviewVerificationRequestDto { Approve = false }, new ReviewVerificationRequestDto { Approve = false });
            responses.Should().OnlyContain(x => x.StatusCode == HttpStatusCode.OK);
        }
        await using var rejected = _fixture.CreateDbContext();
        (await rejected.OrderItemKycStates.Where(x => x.OrderItemId == rejectSeed.V1Item.Id || x.OrderItemId == rejectSeed.V2Item.Id).ToListAsync())
            .Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.Rejected);
    }

    [Fact]
    public async Task Concurrent_rejected_resubmit_is_idempotent_and_final_rejected_does_not_reopen()
    {
        var seed = await SeedAsync();
        var profileId = await PrepareForReviewAsync(seed);
        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
            await service.ReviewAsync(profileId, seed.Admin.Id, new ReviewVerificationRequestDto { Approve = false });
            await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/resubmit-a.jpg", seed.DocumentA.Id, seed.V1Item.Id);
            await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/resubmit-b.jpg", seed.DocumentB.Id, seed.V2Item.Id);
        }

        using (var first = _fixture.CreateClient(seed.UserToken))
        using (var second = _fixture.CreateClient(seed.UserToken))
            (await PostConcurrentlyAsync(first, second, "/api/verification/submit", Request(), Request()))
                .Should().OnlyContain(x => x.IsSuccessStatusCode);

        await using (var resubmitted = _fixture.CreateDbContext())
        {
            (await resubmitted.OrderItemKycStates.Where(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id).ToListAsync())
                .Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.AwaitingReview);
            (await resubmitted.UserVerificationProfiles.CountAsync(x => x.UserId == seed.User.Id)).Should().Be(1);
        }

        await using (var terminal = _fixture.CreateDbContext())
        {
            var states = await terminal.OrderItemKycStates.Where(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id).ToListAsync();
            foreach (var state in states)
                state.Status = (byte)OrderItemKycStatus.FinalRejected;
            await terminal.SaveChangesAsync();
        }

        using (var first = _fixture.CreateClient(seed.UserToken))
        using (var second = _fixture.CreateClient(seed.UserToken))
            (await PostConcurrentlyAsync(first, second, "/api/verification/submit", Request(), Request()))
                .Should().OnlyContain(x => x.IsSuccessStatusCode);

        await using var verifyTerminal = _fixture.CreateDbContext();
        (await verifyTerminal.OrderItemKycStates.Where(x => x.OrderItemId == seed.V1Item.Id || x.OrderItemId == seed.V2Item.Id).ToListAsync())
            .Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.FinalRejected);
        (await verifyTerminal.Orders.SingleAsync(x => x.Id == seed.Order.Id)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
    }

    private static async Task<HttpResponseMessage[]> PostConcurrentlyAsync<TRequest>(
        HttpClient first,
        HttpClient second,
        string path,
        TRequest firstRequest,
        TRequest secondRequest)
    {
        using var gate = new ManualResetEventSlim(false);
        var firstTask = Task.Run(async () =>
        {
            gate.Wait();
            return await first.PostAsJsonAsync(path, firstRequest);
        });
        var secondTask = Task.Run(async () =>
        {
            gate.Wait();
            return await second.PostAsJsonAsync(path, secondRequest);
        });
        gate.Set();
        return await Task.WhenAll(firstTask, secondTask);
    }

    private async Task<Guid> PrepareForReviewAsync((User User, string UserToken, User Admin, string AdminToken, Order Order, OrderItem V1Item, OrderItem V2Item, KycDocumentType DocumentA, KycDocumentType DocumentB) seed)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
        var profile = await service.SubmitAsync(seed.User.Id, Request());
        await service.AddDocumentAsync(seed.User.Id, 1, $"kyc-private:{seed.User.Id:N}/review-a.jpg", seed.DocumentA.Id, seed.V1Item.Id);
        await service.AddDocumentAsync(seed.User.Id, 2, $"kyc-private:{seed.User.Id:N}/review-b.jpg", seed.DocumentB.Id, seed.V2Item.Id);
        await service.SubmitAsync(seed.User.Id, Request());
        return profile.Id;
    }

    private async Task<(User User, string UserToken, User Admin, string AdminToken, Order Order, OrderItem V1Item, OrderItem V2Item, KycDocumentType DocumentA, KycDocumentType DocumentB)> SeedAsync()
    {
        var (user, userToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var now = DateTime.UtcNow;
        var docA = new KycDocumentType { Id = Guid.NewGuid(), Code = $"a-{Guid.NewGuid():N}", Title = "A", IsActive = true, CreatedAt = now };
        var docB = new KycDocumentType { Id = Guid.NewGuid(), Code = $"b-{Guid.NewGuid():N}", Title = "B", IsActive = true, CreatedAt = now };
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p-{Guid.NewGuid():N}", Name = "P", IsActive = true, CreatedAt = now };
        var v1 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V1", CreatedAt = now, PublishedAt = now };
        var v2 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 2, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V2", CreatedAt = now, PublishedAt = now };
        policy.Versions.Add(v1); policy.Versions.Add(v2);
        var category = new Category { Id = Guid.NewGuid(), Title = "P2D", Slug = $"p2d-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "P2D", Slug = $"p2d-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"P2D-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 200m, FinalAmount = 200m, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
        OrderItem Item(Guid version, string title) => new() { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = title, Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = (byte)CurrencyType.Toman, DeliveryType = (byte)DeliveryType.Instant, DeliveryStatus = (byte)DeliveryStatus.Pending, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycEvaluatedAmount = 100m, KycPolicyVersionId = version, CreatedAt = now };
        var item1 = Item(v1.Id, "V1"); var item2 = Item(v2.Id, "V2");
        await using var db = _fixture.CreateDbContext();
        db.AddRange(docA, docB, policy, category, product, order, item1, item2,
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v1.Id, KycDocumentTypeId = docA.Id, IsRequired = true },
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v2.Id, KycDocumentTypeId = docA.Id, IsRequired = true },
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v2.Id, KycDocumentTypeId = docB.Id, IsRequired = true },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item1.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item2.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        return (user, userToken, admin, adminToken, order, item1, item2, docA, docB);
    }

    private static SubmitVerificationRequestDto Request() => new()
    {
        FirstName = "Test", LastName = "User", NationalCode = "1234567890",
        RegisteredMobileBelongsToCardHolder = true
    };
}
