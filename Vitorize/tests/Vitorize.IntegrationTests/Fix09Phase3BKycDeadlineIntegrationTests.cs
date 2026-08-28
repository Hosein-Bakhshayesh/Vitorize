using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Api.Hosting;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase3BKycDeadlineIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase3BKycDeadlineIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Draft_deadline_is_versioned_validated_and_immutable_after_publish()
    {
        var (_, managerToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        var (_, viewerToken) = await _fixture.CreateUserAndTokenAsync("KycViewer");
        using var manager = _fixture.CreateClient(managerToken);
        using var viewer = _fixture.CreateClient(viewerToken);

        var create = await manager.PostAsJsonAsync("/api/admin/kyc/policies", new UpsertKycPolicyRequestDto
        {
            Code = $"deadline-{Guid.NewGuid():N}", Name = "Deadline policy", CustomerTitle = "V1",
            CustomerActionDeadlineHours = 48, IsActive = true
        });
        create.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = (await create.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var v1 = policy.Versions.Should().ContainSingle().Subject;
        v1.CustomerActionDeadlineHours.Should().Be(48);

        (await viewer.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}", new UpdateKycPolicyVersionRequestDto
        {
            CustomerTitle = "viewer", CustomerActionDeadlineHours = 24
        })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await manager.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}", new UpdateKycPolicyVersionRequestDto
        {
            CustomerTitle = "V1", CustomerActionDeadlineHours = 24
        })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await manager.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/publish", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await manager.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}", new UpdateKycPolicyVersionRequestDto
        {
            CustomerTitle = "mutated", CustomerActionDeadlineHours = 12
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await manager.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto
        {
            CustomerTitle = "invalid", CustomerActionDeadlineHours = 0
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Paid_initialization_uses_snapshot_and_paid_at_and_historical_null_remains_null()
    {
        var paidAt = new DateTime(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        await using (var db = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Deadline", Slug = $"deadline-{Guid.NewGuid():N}", IsActive = true, CreatedAt = paidAt };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"deadline-{Guid.NewGuid():N}", Name = "Deadline", IsActive = true, CreatedAt = paidAt };
            var v1 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V1", CustomerActionDeadlineHours = 48, CreatedAt = paidAt, PublishedAt = paidAt };
            var v2 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 2, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V2", CustomerActionDeadlineHours = 24, CreatedAt = paidAt, PublishedAt = paidAt };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Deadline product", Slug = $"deadline-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = paidAt };
            var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"D-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 2, FinalAmount = 2, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = paidAt.AddDays(-2), PaidAt = paidAt };
            var v1Item = Item(order, product, v1.Id, 48, true, paidAt);
            var v2Item = Item(order, product, v2.Id, 24, true, paidAt);
            var historical = Item(order, product, v1.Id, null, true, paidAt);
            db.AddRange(category, policy, v1, v2, product, order, v1Item, v2Item, historical);
            await db.SaveChangesAsync();

            using var scope = _fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPostPaymentOrderProcessor>().ProcessPaidOrderAsync(order.Id);

            await db.Entry(order).ReloadAsync();
            var states = await db.OrderItemKycStates.Where(x => x.OrderItemId == v1Item.Id || x.OrderItemId == v2Item.Id || x.OrderItemId == historical.Id).ToListAsync();
            states.Single(x => x.OrderItemId == v1Item.Id).CustomerActionDeadlineAt.Should().Be(paidAt.AddHours(48));
            states.Single(x => x.OrderItemId == v2Item.Id).CustomerActionDeadlineAt.Should().Be(paidAt.AddHours(24));
            states.Single(x => x.OrderItemId == historical.Id).CustomerActionDeadlineAt.Should().BeNull();
        }
    }

    [Fact]
    public async Task Rejection_resets_the_deadline_and_projection_is_overdue_without_mutating_state()
    {
        var rejectedAt = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        Guid orderId;
        Guid itemId;
        await using (var setup = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Lifecycle deadline", Slug = $"lifecycle-deadline-{Guid.NewGuid():N}", IsActive = true, CreatedAt = rejectedAt };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"lifecycle-deadline-{Guid.NewGuid():N}", Name = "Lifecycle deadline", IsActive = true, CreatedAt = rejectedAt };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Lifecycle deadline", CustomerActionDeadlineHours = 24, CreatedAt = rejectedAt, PublishedAt = rejectedAt };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Lifecycle", Slug = $"lifecycle-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = rejectedAt };
            var order = new Order { Id = Guid.NewGuid(), UserId = user.Id, OrderNumber = $"L-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 1, FinalAmount = 1, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = rejectedAt, PaidAt = rejectedAt };
            var item = Item(order, product, version.Id, 24, true, rejectedAt);
            var state = new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.AwaitingReview, CreatedAt = rejectedAt, UpdatedAt = rejectedAt, CustomerActionDeadlineAt = rejectedAt.AddHours(-1) };
            setup.AddRange(category, policy, version, product, order, item, state);
            await setup.SaveChangesAsync();
            orderId = order.Id;
            itemId = item.Id;
        }

        await using (var transitionDb = _fixture.CreateDbContext())
        {
            var coordinator = new OrderItemKycLifecycleCoordinator(transitionDb, timeProvider: new FixedTimeProvider(rejectedAt));
            await coordinator.SynchronizeReviewAsync(user.Id, Guid.NewGuid(), approved: false);
            await transitionDb.SaveChangesAsync();
        }

        using (var scope = _fixture.Factory.Services.CreateScope())
        {
            var projection = await scope.ServiceProvider.GetRequiredService<IOrderService>().GetMyOrderDetailsAsync(user.Id, orderId);
            var kyc = projection.Items.Single(x => x.Id == itemId).Kyc!;
            kyc.CustomerActionDeadlineHours.Should().Be(24);
            kyc.CustomerActionDeadlineAt.Should().Be(rejectedAt.AddHours(24));
            kyc.IsCustomerActionOverdue.Should().BeTrue();
        }

        await using var verify = _fixture.CreateDbContext();
        var persisted = await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == itemId);
        persisted.Status.Should().Be((byte)OrderItemKycStatus.Rejected);
        persisted.CustomerActionDeadlineAt.Should().Be(rejectedAt.AddHours(24));
    }

    [Fact]
    public async Task Deadline_worker_and_admin_operations_are_authoritative_and_idempotent()
    {
        var now = DateTime.UtcNow.AddMinutes(-5);
        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (manager, managerToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (_, viewerToken) = await _fixture.CreateUserAndTokenAsync("KycViewer");
        Guid itemId;

        await using (var setup = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Expiry", Slug = $"expiry-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"expiry-{Guid.NewGuid():N}", Name = "Expiry", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Expiry", CustomerActionDeadlineHours = 24, CreatedAt = now, PublishedAt = now };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Expiry", Slug = $"expiry-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var order = new Order { Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"E-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 1, FinalAmount = 1, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
            var item = Item(order, product, version.Id, 24, true, now);
            itemId = item.Id;
            setup.AddRange(category, policy, version, product, order, item, new OrderItemKycState
            {
                Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission,
                CustomerActionDeadlineAt = now.AddMinutes(-1), CreatedAt = now, UpdatedAt = now
            });
            await setup.SaveChangesAsync();
        }

        // The command path is the security boundary: it expires the row in the
        // same transaction and must not create a partial verification profile.
        using (var customerClient = _fixture.CreateClient(customerToken))
        {
            var submit = await customerClient.PostAsJsonAsync("/api/verification/submit", new SubmitVerificationRequestDto
            {
                FirstName = "Expiry", LastName = "Customer", NationalCode = "1234567890",
                RegisteredMobileBelongsToCardHolder = true
            });
            submit.StatusCode.Should().Be(HttpStatusCode.Conflict);
        }
        await using (var verifyCommand = _fixture.CreateDbContext())
        {
            (await verifyCommand.UserVerificationProfiles.CountAsync(x => x.UserId == customer.Id)).Should().Be(0);
            var state = await verifyCommand.OrderItemKycStates.SingleAsync(x => x.OrderItemId == itemId);
            state.Status.Should().Be((byte)OrderItemKycStatus.Expired);
            state.Status = (byte)OrderItemKycStatus.AwaitingSubmission;
            state.CustomerActionDeadlineAt = DateTime.UtcNow.AddMinutes(-1);
            await verifyCommand.SaveChangesAsync();
        }

        await using (var workerDb = _fixture.CreateDbContext())
        {
            var service = new OrderItemKycDeadlineService(workerDb, new FixedTimeProvider(DateTime.UtcNow));
            (await service.ProcessOverdueBatchAsync(10)).Should().Be(1);
        }

        await using (var verifyExpiry = _fixture.CreateDbContext())
        {
            var state = await verifyExpiry.OrderItemKycStates.SingleAsync(x => x.OrderItemId == itemId);
            state.Status.Should().Be((byte)OrderItemKycStatus.Expired);
            state.CustomerActionDeadlineAt.Should().BeNull();
        }

        using var viewer = _fixture.CreateClient(viewerToken);
        var future = DateTime.UtcNow.AddHours(2);
        (await viewer.PostAsJsonAsync($"/api/admin/verifications/order-items/{itemId}/reopen", new { NewDeadlineAt = future }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        using var manage = _fixture.CreateClient(managerToken);
        var reopened = await manage.PostAsJsonAsync($"/api/admin/verifications/order-items/{itemId}/reopen", new { NewDeadlineAt = future });
        reopened.StatusCode.Should().Be(HttpStatusCode.OK);
        var reopenPayload = (await reopened.Content.ReadFromJsonAsync<ApiResult<OrderItemKycDeadlineOperationDto>>())!.Data!;
        reopenPayload.LifecycleStatus.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
        reopenPayload.CustomerActionDeadlineAt.Should().BeCloseTo(future, TimeSpan.FromSeconds(1));

        var same = await manage.PutAsJsonAsync($"/api/admin/verifications/order-items/{itemId}/deadline", new { NewDeadlineAt = future });
        same.StatusCode.Should().Be(HttpStatusCode.OK);
        (await same.Content.ReadFromJsonAsync<ApiResult<OrderItemKycDeadlineOperationDto>>())!.Data!.Changed.Should().BeFalse();

        var later = future.AddHours(1);
        var extended = await manage.PutAsJsonAsync($"/api/admin/verifications/order-items/{itemId}/deadline", new { NewDeadlineAt = later });
        extended.StatusCode.Should().Be(HttpStatusCode.OK);
        (await extended.Content.ReadFromJsonAsync<ApiResult<OrderItemKycDeadlineOperationDto>>())!.Data!.Changed.Should().BeTrue();
    }

    [Fact]
    public async Task Upload_blocks_effective_and_persisted_expiry_without_writing_private_files_or_documents()
    {
        var now = DateTime.UtcNow;
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        Guid effectiveItemId;
        Guid persistedItemId;
        await using (var setup = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Upload deadline", Slug = $"upload-deadline-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"upload-deadline-{Guid.NewGuid():N}", Name = "Upload deadline", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Upload deadline", CustomerActionDeadlineHours = 24, CreatedAt = now, PublishedAt = now };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Upload deadline", Slug = $"upload-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var order = new Order { Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"U-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 2, FinalAmount = 2, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
            var effective = Item(order, product, version.Id, 24, true, now);
            var persisted = Item(order, product, version.Id, 24, true, now);
            setup.AddRange(category, policy, version, product, order, effective, persisted,
                new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = effective.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CustomerActionDeadlineAt = now.AddMinutes(-1), CreatedAt = now, UpdatedAt = now },
                new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = persisted.Id, Status = (byte)OrderItemKycStatus.Expired, CreatedAt = now, UpdatedAt = now });
            await setup.SaveChangesAsync();
            effectiveItemId = effective.Id;
            persistedItemId = persisted.Id;
        }

        var privateRoot = _fixture.Factory.Services.GetRequiredService<HostingStoragePaths>().PrivateDocumentsRoot;
        var customerRoot = Path.Combine(privateRoot, customer.Id.ToString("N"));
        if (Directory.Exists(customerRoot)) Directory.Delete(customerRoot, recursive: true);

        using var client = _fixture.CreateClient(token);
        (await UploadAsync(client, effectiveItemId)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await UploadAsync(client, persistedItemId)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == effectiveItemId)).Status.Should().Be((byte)OrderItemKycStatus.Expired);
        (await verify.VerificationDocuments.CountAsync(x => x.UserVerificationProfile.UserId == customer.Id)).Should().Be(0);
        Directory.Exists(customerRoot).Should().BeFalse();
    }

    [Fact]
    public async Task Expiry_batch_converges_more_candidates_than_batch_size_without_touching_review_or_no_deadline_rows()
    {
        var now = DateTime.UtcNow;
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var expiredItemIds = new List<Guid>();
        Guid awaitingReviewItemId;
        Guid noDeadlineItemId;
        await using (var setup = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "Batch deadline", Slug = $"batch-deadline-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
            var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"batch-deadline-{Guid.NewGuid():N}", Name = "Batch deadline", IsActive = true, CreatedAt = now };
            var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Batch deadline", CustomerActionDeadlineHours = 24, CreatedAt = now, PublishedAt = now };
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Batch deadline", Slug = $"batch-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 1, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
            var order = new Order { Id = Guid.NewGuid(), UserId = customer.Id, OrderNumber = $"B-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 14, FinalAmount = 14, CurrencyType = (byte)CurrencyType.Toman, CreatedAt = now, PaidAt = now };
            setup.AddRange(category, policy, version, product, order);
            for (var index = 0; index < 12; index++)
            {
                var item = Item(order, product, version.Id, 24, true, now);
                expiredItemIds.Add(item.Id);
                setup.AddRange(item, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CustomerActionDeadlineAt = now.AddMinutes(-1), CreatedAt = now, UpdatedAt = now });
            }
            var reviewing = Item(order, product, version.Id, 24, true, now);
            var noDeadline = Item(order, product, version.Id, null, true, now);
            awaitingReviewItemId = reviewing.Id;
            noDeadlineItemId = noDeadline.Id;
            setup.AddRange(reviewing, noDeadline,
                new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = reviewing.Id, Status = (byte)OrderItemKycStatus.AwaitingReview, CustomerActionDeadlineAt = now.AddMinutes(-1), CreatedAt = now, UpdatedAt = now },
                new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = noDeadline.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now });
            await setup.SaveChangesAsync();
        }

        var changed = 0;
        await using (var workerDb = _fixture.CreateDbContext())
        {
            var service = new OrderItemKycDeadlineService(workerDb, new FixedTimeProvider(now));
            while (true)
            {
                var batch = await service.ProcessOverdueBatchAsync(5);
                changed += batch;
                if (batch == 0) break;
            }
        }

        changed.Should().Be(12);
        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemKycStates.Where(x => expiredItemIds.Contains(x.OrderItemId)).Select(x => x.Status).ToListAsync())
            .Should().OnlyContain(status => status == (byte)OrderItemKycStatus.Expired);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == awaitingReviewItemId)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingReview);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == noDeadlineItemId)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
    }

    private static Task<HttpResponseMessage> UploadAsync(HttpClient client, Guid itemId)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 }) { Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg") } }, "file", "identity.jpg");
        return client.PostAsync($"/api/uploads/verification-document?orderItemId={itemId}", form);
    }

    private static OrderItem Item(Order order, Product product, Guid policyVersionId, int? deadlineHours, bool requiresKyc, DateTime now) => new()
    {
        Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title,
        Quantity = 1, UnitPrice = 1, TotalPrice = 1, CurrencyType = (byte)CurrencyType.Toman,
        DeliveryType = (byte)DeliveryType.Manual, DeliveryStatus = (byte)DeliveryStatus.Pending,
        RequiresVerification = requiresKyc, KycRequirementMode = (byte)KycRequirementMode.Always,
        KycEvaluatedAmount = 1, KycPolicyVersionId = policyVersionId,
        KycCustomerActionDeadlineHours = deadlineHours, CreatedAt = now
    };

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow);
    }
}
