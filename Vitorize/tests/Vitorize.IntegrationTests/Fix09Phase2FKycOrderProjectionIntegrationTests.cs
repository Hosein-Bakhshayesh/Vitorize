using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>Real API evidence for the Phase-2F item-level KYC order projection.</summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2FKycOrderProjectionIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix09Phase2FKycOrderProjectionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Customer_projection_maps_all_lifecycle_states_and_actions()
    {
        var seed = await SeedAsync();
        using var client = _fixture.CreateClient(seed.OwnerToken);
        var detail = await GetOrderAsync(client, seed.MainOrderId);

        AssertKyc(detail, seed.AwaitingSubmission, OrderItemKycStatus.AwaitingSubmission, true, "SubmitVerification", false);
        AssertKyc(detail, seed.AwaitingReview, OrderItemKycStatus.AwaitingReview, true, "AwaitReview", false);
        AssertKyc(detail, seed.Rejected, OrderItemKycStatus.Rejected, true, "ResubmitVerification", false);
        AssertKyc(detail, seed.FinalRejected, OrderItemKycStatus.FinalRejected, true, "NoFurtherSubmission", false);
        AssertKyc(detail, seed.SatisfiedDelivered, OrderItemKycStatus.Satisfied, false, "None", true);
        AssertKyc(detail, seed.SatisfiedManual, OrderItemKycStatus.Satisfied, false, "AwaitManualDelivery", false);
        AssertKyc(detail, seed.SatisfiedSupport, OrderItemKycStatus.Satisfied, false, "AwaitSupportFulfillment", false);
        AssertKyc(detail, seed.NotRequired, OrderItemKycStatus.NotRequired, false, "None", false);
        detail.Items.Single(x => x.Id == seed.Legacy).Kyc.Should().BeNull("a historical item must not get a fabricated lifecycle");
    }

    [Fact]
    public async Task Projection_uses_purchase_time_policy_requirements_and_upload_presence()
    {
        var seed = await SeedAsync();
        using var client = _fixture.CreateClient(seed.OwnerToken);
        var v1 = await GetOrderAsync(client, seed.MainOrderId);
        var v2 = await GetOrderAsync(client, seed.V2OrderId);

        var v1Kyc = v1.Items.Single(x => x.Id == seed.AwaitingSubmission).Kyc!;
        v1Kyc.PolicyVersionId.Should().Be(seed.V1PolicyVersionId);
        v1Kyc.PolicyTitle.Should().Be("Phase 2F V1");
        v1Kyc.PolicyInstructions.Should().Be("V1 purchase-time instructions");
        v1Kyc.Documents.Should().ContainSingle(x => x.DocumentTypeId == seed.DocumentAId && x.IsRequired && x.UploadStatus == "Uploaded");
        v1Kyc.Documents.Should().ContainSingle(x => x.DocumentTypeId == seed.OptionalDocumentId && !x.IsRequired && x.UploadStatus == "Missing");
        v1Kyc.Documents.Should().NotContain(x => x.DocumentTypeId == seed.DocumentBId);

        var v2Kyc = v2.Items.Single(x => x.Id == seed.ReleasePending).Kyc!;
        v2Kyc.PolicyVersionId.Should().Be(seed.V2PolicyVersionId);
        v2Kyc.PolicyTitle.Should().Be("Phase 2F V2");
        v2Kyc.PolicyInstructions.Should().Be("V2 purchase-time instructions");
        v2Kyc.Documents.Should().Contain(x => x.DocumentTypeId == seed.DocumentAId && x.IsRequired && x.UploadStatus == "Uploaded");
        v2Kyc.Documents.Should().Contain(x => x.DocumentTypeId == seed.DocumentBId && x.IsRequired && x.UploadStatus == "Missing");
        v2Kyc.BlocksFulfillment.Should().BeFalse("a missing optional/required upload is not inferred from a satisfied item snapshot");
        AssertKyc(v2, seed.ReleasePending, OrderItemKycStatus.Satisfied, false, "AwaitFulfillment", false);
    }

    [Fact]
    public async Task Kyc_context_uses_historical_context_and_enforces_order_item_ownership()
    {
        var seed = await SeedAsync();
        using var owner = _fixture.CreateClient(seed.OwnerToken);
        using var other = _fixture.CreateClient(seed.OtherToken);

        var v1 = await GetContextAsync(owner, seed.AwaitingSubmission);
        v1.PolicyVersionId.Should().Be(seed.V1PolicyVersionId);
        v1.PolicyTitle.Should().Be("Phase 2F V1");
        v1.Documents.Should().NotContain(x => x.DocumentTypeId == seed.DocumentBId);

        var v2 = await GetContextAsync(owner, seed.ReleasePending);
        v2.PolicyVersionId.Should().Be(seed.V2PolicyVersionId);
        v2.Documents.Should().Contain(x => x.DocumentTypeId == seed.DocumentBId && x.IsRequired);

        var rejected = await GetContextAsync(owner, seed.Rejected);
        rejected.CustomerAction.Should().Be("ResubmitVerification");
        (await owner.GetAsync($"/api/orders/items/{seed.FinalRejected}/kyc-context")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await other.GetAsync($"/api/orders/{seed.MainOrderId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await other.GetAsync($"/api/orders/items/{seed.AwaitingSubmission}/kyc-context")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Customer_projection_does_not_leak_held_code_or_verification_internals()
    {
        var seed = await SeedAsync();
        using var client = _fixture.CreateClient(seed.OwnerToken);
        var response = await client.GetAsync($"/api/orders/{seed.V2OrderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();

        json.Should().NotContain(seed.HeldGiftCodeSecret);
        json.Should().NotContain(seed.DocumentStoragePath);
        json.Should().NotContain(seed.AdminNote);
        json.Contains("rowVersion", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("financialAudit", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        json.Contains("encryptedPayload", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public async Task Admin_projection_contains_operational_kyc_state_for_review_satisfied_and_final_rejected()
    {
        var seed = await SeedAsync();
        using var client = _fixture.CreateClient(seed.AdminToken);
        var detail = await GetOrderAsync(client, seed.MainOrderId, admin: true);

        foreach (var id in new[] { seed.AwaitingReview, seed.SatisfiedManual, seed.FinalRejected })
        {
            var item = detail.Items.Single(x => x.Id == id);
            item.Kyc.Should().NotBeNull();
            item.Kyc!.PolicyVersionId.Should().Be(seed.V1PolicyVersionId);
            item.Kyc.EvaluatedAmount.Should().Be(250m);
            item.Kyc.ThresholdAmount.Should().Be(200m);
        }
        AssertKyc(detail, seed.AwaitingReview, OrderItemKycStatus.AwaitingReview, true, "AwaitReview", false);
        AssertKyc(detail, seed.FinalRejected, OrderItemKycStatus.FinalRejected, true, "NoFurtherSubmission", false);
    }

    [Fact]
    public async Task Mixed_order_preserves_each_item_kyc_truth_and_legacy_item_stays_unset()
    {
        var seed = await SeedAsync();
        using var client = _fixture.CreateClient(seed.OwnerToken);
        var detail = await GetOrderAsync(client, seed.MixedOrderId);

        detail.Items.Should().HaveCount(4);
        detail.Items.Single(x => x.Id == seed.MixedDeliveredLegacy).Kyc.Should().BeNull();
        AssertKyc(detail, seed.MixedAwaitingSubmission, OrderItemKycStatus.AwaitingSubmission, true, "SubmitVerification", false);
        AssertKyc(detail, seed.MixedAwaitingReview, OrderItemKycStatus.AwaitingReview, true, "AwaitReview", false);
        AssertKyc(detail, seed.MixedManual, OrderItemKycStatus.Satisfied, false, "AwaitManualDelivery", false);
    }

    private static void AssertKyc(OrderDto detail, Guid itemId, OrderItemKycStatus status, bool blocks, string action, bool fulfilled)
    {
        var kyc = detail.Items.Single(x => x.Id == itemId).Kyc;
        kyc.Should().NotBeNull();
        kyc!.LifecycleStatus.Should().Be((byte)status);
        kyc.BlocksFulfillment.Should().Be(blocks);
        kyc.CustomerAction.Should().Be(action);
        kyc.IsFulfilled.Should().Be(fulfilled);
    }

    private static async Task<OrderDto> GetOrderAsync(HttpClient client, Guid orderId, bool admin = false)
    {
        var response = await client.GetAsync(admin ? $"/api/admin/orders/{orderId}" : $"/api/orders/{orderId}");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;
    }

    private static async Task<OrderItemKycProjectionDto> GetContextAsync(HttpClient client, Guid itemId)
    {
        var response = await client.GetAsync($"/api/orders/items/{itemId}/kyc-context");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<OrderItemKycProjectionDto>>())!.Data!;
    }

    private async Task<Seed> SeedAsync()
    {
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var scope = _fixture.Factory.Services.CreateScope();
        var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var now = DateTime.UtcNow;
        var storagePath = $"kyc-private:phase2f/{Guid.NewGuid():N}/document.jpg";
        var note = $"PHASE2F-ADMIN-NOTE-{Guid.NewGuid():N}";
        var heldSecret = $"PHASE2F-HELD-CODE-{Guid.NewGuid():N}";
        var documentA = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p2f-a-{Guid.NewGuid():N}", Title = "Document A", IsActive = true, CreatedAt = now };
        var documentB = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p2f-b-{Guid.NewGuid():N}", Title = "Document B", IsActive = true, CreatedAt = now };
        var optional = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p2f-o-{Guid.NewGuid():N}", Title = "Optional document", IsActive = true, CreatedAt = now };
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p2f-{Guid.NewGuid():N}", Name = "Phase 2F", IsActive = true, CreatedAt = now };
        var v1 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Phase 2F V1", CustomerInstructions = "V1 purchase-time instructions", CreatedAt = now, PublishedAt = now };
        var v2 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 2, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Phase 2F V2", CustomerInstructions = "V2 purchase-time instructions", CreatedAt = now, PublishedAt = now };
        policy.Versions.Add(v1); policy.Versions.Add(v2);
        var category = new Category { Id = Guid.NewGuid(), Title = "Phase 2F", Slug = $"p2f-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        // The product deliberately points to V2; V1 items below must still project V1.
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Phase 2F product", Slug = $"p2f-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Instant, BasePrice = 250m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.AboveThreshold, KycThresholdAmount = 200m, KycPolicyVersionId = v2.Id, CreatedAt = now };
        Order Order(string suffix, string? adminNote = null) => new() { Id = Guid.NewGuid(), UserId = owner.Id, OrderNumber = $"P2F-{suffix}-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 1_000m, FinalAmount = 1_000m, CurrencyType = (byte)CurrencyType.Toman, AdminNote = adminNote, CreatedAt = now, PaidAt = now };
        var main = Order("MAIN", note); var v2Order = Order("V2"); var mixed = Order("MIXED");
        OrderItem Item(Order order, string title, DeliveryType deliveryType, Guid? policyId, OrderItemKycStatus? status, bool delivered = false) => new()
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = title, Quantity = 1, UnitPrice = 250m, TotalPrice = 250m, CurrencyType = (byte)CurrencyType.Toman,
            DeliveryType = (byte)deliveryType, DeliveryStatus = (byte)(delivered ? DeliveryStatus.Delivered : DeliveryStatus.Pending), DeliveredAt = delivered ? now : null,
            RequiresVerification = status != OrderItemKycStatus.NotRequired && status is not null, KycRequirementMode = (byte)(status == OrderItemKycStatus.NotRequired || status is null ? KycRequirementMode.None : KycRequirementMode.AboveThreshold), KycPolicyVersionId = policyId,
            KycThresholdAmount = policyId is null ? null : 200m, KycEvaluatedAmount = policyId is null ? 0m : 250m, CreatedAt = now
        };
        var awaitingSubmission = Item(main, "Awaiting submission", DeliveryType.Instant, v1.Id, OrderItemKycStatus.AwaitingSubmission);
        var awaitingReview = Item(main, "Awaiting review", DeliveryType.Instant, v1.Id, OrderItemKycStatus.AwaitingReview);
        var rejected = Item(main, "Rejected", DeliveryType.Instant, v1.Id, OrderItemKycStatus.Rejected);
        var finalRejected = Item(main, "Final rejected", DeliveryType.Instant, v1.Id, OrderItemKycStatus.FinalRejected);
        var satisfiedDelivered = Item(main, "Delivered", DeliveryType.Instant, v1.Id, OrderItemKycStatus.Satisfied, delivered: true);
        var satisfiedManual = Item(main, "Manual", DeliveryType.Manual, v1.Id, OrderItemKycStatus.Satisfied);
        var satisfiedSupport = Item(main, "Support", DeliveryType.SupportRequired, v1.Id, OrderItemKycStatus.Satisfied);
        var notRequired = Item(main, "Not required", DeliveryType.Manual, null, OrderItemKycStatus.NotRequired);
        var legacy = Item(main, "Legacy", DeliveryType.Manual, null, null);
        var releasePending = Item(v2Order, "Release pending", DeliveryType.Instant, v2.Id, OrderItemKycStatus.Satisfied);
        var mixedDeliveredLegacy = Item(mixed, "Mixed legacy delivered", DeliveryType.Instant, null, null, delivered: true);
        var mixedAwaitingSubmission = Item(mixed, "Mixed submission", DeliveryType.Instant, v1.Id, OrderItemKycStatus.AwaitingSubmission);
        var mixedAwaitingReview = Item(mixed, "Mixed review", DeliveryType.Instant, v1.Id, OrderItemKycStatus.AwaitingReview);
        var mixedManual = Item(mixed, "Mixed manual", DeliveryType.Manual, v1.Id, OrderItemKycStatus.Satisfied);
        var supportTicket = new Ticket { Id = Guid.NewGuid(), UserId = owner.Id, OrderId = main.Id, Subject = "Phase 2F support", Department = (byte)TicketDepartment.Orders, Priority = 1, Status = (byte)TicketStatus.Open, IsFulfillmentTicket = true, CreatedAt = now };
        satisfiedSupport.SupportTicketId = supportTicket.Id;
        var profile = new UserVerificationProfile { Id = Guid.NewGuid(), UserId = owner.Id, FirstName = "Phase", LastName = "TwoF", NationalCode = "1234567890", Status = (byte)VerificationStatus.Pending, AdminNote = note, EncryptedPayload = "phase2f-encrypted-payload", CreatedAt = now };
        var deliveredCode = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = satisfiedDelivered.Id, EncryptedCode = crypto.Encrypt("P2F-DELIVERED-NONCANARY"), MaskedCode = "****2F", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Delivered, ReservedByUserId = owner.Id, SoldAt = now, DeliveredAt = now, CreatedAt = now };
        var heldCode = new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, OrderItemId = releasePending.Id, EncryptedCode = crypto.Encrypt(heldSecret), MaskedCode = "****HELD", CodeHashFingerprint = Guid.NewGuid().ToString("N"), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Sold, ReservedByUserId = owner.Id, SoldAt = now, CreatedAt = now };

        await using var db = _fixture.CreateDbContext();
        db.AddRange(documentA, documentB, optional, policy, category, product, main, v2Order, mixed, supportTicket, profile,
            awaitingSubmission, awaitingReview, rejected, finalRejected, satisfiedDelivered, satisfiedManual, satisfiedSupport, notRequired, legacy, releasePending,
            mixedDeliveredLegacy, mixedAwaitingSubmission, mixedAwaitingReview, mixedManual, deliveredCode, heldCode,
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v1.Id, KycDocumentTypeId = documentA.Id, IsRequired = true, SortOrder = 1, Instructions = "A required" },
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v1.Id, KycDocumentTypeId = optional.Id, IsRequired = false, SortOrder = 2, Instructions = "Optional" },
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v2.Id, KycDocumentTypeId = documentA.Id, IsRequired = true, SortOrder = 1 },
            new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = v2.Id, KycDocumentTypeId = documentB.Id, IsRequired = true, SortOrder = 2 },
            new VerificationDocument { Id = Guid.NewGuid(), UserVerificationProfileId = profile.Id, DocumentType = 1, KycDocumentTypeId = documentA.Id, FilePath = storagePath, Status = 1, AdminNote = note, CreatedAt = now },
            new OrderItemDelivery { Id = Guid.NewGuid(), OrderItemId = satisfiedDelivered.Id, DeliveryType = (byte)DeliveryType.Instant, GiftCodeId = deliveredCode.Id, DeliveredContent = crypto.Encrypt("P2F-DELIVERED-NONCANARY"), ContentHash = new string('A', 64), EncryptionVersion = 2, IsVisibleToCustomer = true, CreatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = awaitingSubmission.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = awaitingReview.Id, Status = (byte)OrderItemKycStatus.AwaitingReview, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = rejected.Id, Status = (byte)OrderItemKycStatus.Rejected, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = finalRejected.Id, Status = (byte)OrderItemKycStatus.FinalRejected, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = satisfiedDelivered.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = satisfiedManual.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = satisfiedSupport.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = notRequired.Id, Status = (byte)OrderItemKycStatus.NotRequired, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = releasePending.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = mixedAwaitingSubmission.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = mixedAwaitingReview.Id, Status = (byte)OrderItemKycStatus.AwaitingReview, CreatedAt = now, UpdatedAt = now },
            new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = mixedManual.Id, Status = (byte)OrderItemKycStatus.Satisfied, CreatedAt = now, UpdatedAt = now, SatisfiedAt = now });
        await db.SaveChangesAsync();

        return new Seed(ownerToken, otherToken, adminToken, main.Id, v2Order.Id, mixed.Id, v1.Id, v2.Id, documentA.Id, documentB.Id, optional.Id,
            awaitingSubmission.Id, awaitingReview.Id, rejected.Id, finalRejected.Id, satisfiedDelivered.Id, satisfiedManual.Id, satisfiedSupport.Id, notRequired.Id, legacy.Id,
            releasePending.Id, mixedDeliveredLegacy.Id, mixedAwaitingSubmission.Id, mixedAwaitingReview.Id, mixedManual.Id, heldSecret, storagePath, note);
    }

    private sealed record Seed(string OwnerToken, string OtherToken, string AdminToken, Guid MainOrderId, Guid V2OrderId, Guid MixedOrderId,
        Guid V1PolicyVersionId, Guid V2PolicyVersionId, Guid DocumentAId, Guid DocumentBId, Guid OptionalDocumentId,
        Guid AwaitingSubmission, Guid AwaitingReview, Guid Rejected, Guid FinalRejected, Guid SatisfiedDelivered, Guid SatisfiedManual, Guid SatisfiedSupport,
        Guid NotRequired, Guid Legacy, Guid ReleasePending, Guid MixedDeliveredLegacy, Guid MixedAwaitingSubmission, Guid MixedAwaitingReview, Guid MixedManual,
        string HeldGiftCodeSecret, string DocumentStoragePath, string AdminNote);
}
