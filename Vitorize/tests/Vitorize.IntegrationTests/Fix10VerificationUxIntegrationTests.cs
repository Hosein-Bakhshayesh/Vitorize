using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix10VerificationUxIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    private static readonly DateTimeOffset Today = new(2026, 8, 15, 9, 0, 0, TimeSpan.Zero);

    public Fix10VerificationUxIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Dob_boundaries_and_valid_leap_day_are_accepted()
    {
        foreach (var birthDate in new[] { new DateOnly(1900, 1, 1), new DateOnly(2026, 8, 15), new DateOnly(2024, 2, 29) })
        {
            var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
            using var scope = _fixture.Factory.Services.CreateScope();
            var service = Service(scope, Today);
            await AddGenericDocumentsAsync(service, user.Id);
            var result = await service.SubmitAsync(user.Id, Request(birthDate));
            result.BirthDate.Should().Be(birthDate);
        }
    }

    [Fact]
    public async Task Future_and_too_old_dobs_are_rejected_without_a_profile_mutation()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = Service(scope, Today);

        foreach (var birthDate in new[] { new DateOnly(2026, 8, 16), new DateOnly(2099, 2, 28), new DateOnly(1899, 12, 31) })
            await service.Invoking(x => x.SubmitAsync(user.Id, Request(birthDate))).Should().ThrowAsync<BusinessException>();

        await using var verify = _fixture.CreateDbContext();
        (await verify.UserVerificationProfiles.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Impossible_iso_date_remains_a_model_binding_bad_request()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        using var content = new StringContent("{\"firstName\":\"Test\",\"lastName\":\"User\",\"nationalCode\":\"1234567890\",\"birthDate\":\"2025-02-29\"}", System.Text.Encoding.UTF8, "application/json");
        (await client.PostAsync("/api/verification/submit", content)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Draft_document_instructions_persist_project_to_customer_and_remain_version_isolated_after_publish()
    {
        var (admin, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var adminClient = _fixture.CreateClient(adminToken);
        var docA = await CreateDocumentAsync(adminClient, "Required");
        var docB = await CreateDocumentAsync(adminClient, "Optional");
        var policyResponse = await adminClient.PostAsJsonAsync("/api/admin/kyc/policies", new UpsertKycPolicyRequestDto
        {
            Code = $"fix10-{Guid.NewGuid():N}", Name = "FIX10", CustomerTitle = "FIX10 Customer"
        });
        policyResponse.EnsureSuccessStatusCode();
        var policy = (await policyResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var v1 = policy.Versions.Should().ContainSingle().Which;
        var v1Instructions = "خط اول راهنما\nخط دوم راهنما";
        var requirements = new SetKycPolicyDocumentRequirementsRequestDto
        {
            Requirements =
            [
                new() { KycDocumentTypeId = docA.Id, IsRequired = true, SortOrder = 10, CustomerInstructions = v1Instructions },
                new() { KycDocumentTypeId = docB.Id, IsRequired = false, SortOrder = 20, CustomerInstructions = "راهنمای مدرک اختیاری" }
            ]
        };
        (await adminClient.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", requirements)).EnsureSuccessStatusCode();
        var readDraft = (await (await adminClient.GetAsync($"/api/admin/kyc/policy-versions/{v1.Id}")).Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;
        readDraft.DocumentRequirements.Should().Contain(x => x.KycDocumentTypeId == docA.Id && x.CustomerInstructions == v1Instructions);
        readDraft.DocumentRequirements.Should().Contain(x => x.KycDocumentTypeId == docB.Id && !x.IsRequired && x.CustomerInstructions == "راهنمای مدرک اختیاری");

        (await adminClient.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/publish", new { })).EnsureSuccessStatusCode();
        (await adminClient.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto())).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var v2Response = await adminClient.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto { CustomerTitle = "FIX10 V2" });
        v2Response.EnsureSuccessStatusCode();
        var v2 = (await v2Response.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;
        (await adminClient.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto
        {
            Requirements = [new() { KycDocumentTypeId = docA.Id, IsRequired = true, SortOrder = 10, CustomerInstructions = "V2 instruction" }]
        })).EnsureSuccessStatusCode();

        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var itemId = await SeedPurchasedItemAsync(customer.Id, v1.Id);
        using var customerClient = _fixture.CreateClient(customerToken);
        var context = (await (await customerClient.GetAsync($"/api/orders/items/{itemId}/kyc-context")).Content.ReadFromJsonAsync<ApiResult<OrderItemKycProjectionDto>>())!.Data!;
        context.Documents.Should().Contain(x => x.DocumentTypeId == docA.Id && x.Instructions == v1Instructions && x.IsRequired);
        context.Documents.Should().Contain(x => x.DocumentTypeId == docB.Id && x.Instructions == "راهنمای مدرک اختیاری" && !x.IsRequired);
        (await (await adminClient.GetAsync($"/api/admin/kyc/policy-versions/{v2.Id}")).Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!
            .DocumentRequirements.Should().ContainSingle(x => x.CustomerInstructions == "V2 instruction");
    }

    private VerificationService Service(IServiceScope scope, DateTimeOffset now) =>
        ActivatorUtilities.CreateInstance<VerificationService>(scope.ServiceProvider, new FixedTimeProvider(now));

    private static SubmitVerificationRequestDto Request(DateOnly birthDate) => new()
    {
        FirstName = "Test", LastName = "User", NationalCode = "1234567890", BirthDate = birthDate,
        RegisteredMobileBelongsToCardHolder = true
    };

    private static async Task AddGenericDocumentsAsync(IVerificationService service, Guid userId)
    {
        await service.AddDocumentAsync(userId, 1, $"kyc-private:{userId:N}/identity.jpg");
        await service.AddDocumentAsync(userId, 4, $"kyc-private:{userId:N}/card.jpg");
    }

    private static async Task<AdminKycDocumentTypeDto> CreateDocumentAsync(HttpClient client, string suffix)
    {
        var response = await client.PostAsJsonAsync("/api/admin/kyc/document-types", new UpsertKycDocumentTypeRequestDto
        {
            Code = $"fix10-{suffix}-{Guid.NewGuid():N}", Title = $"FIX10 {suffix}", IsActive = true,
            AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 1_024, SortOrder = 10
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminKycDocumentTypeDto>>())!.Data!;
    }

    private async Task<Guid> SeedPurchasedItemAsync(Guid userId, Guid versionId)
    {
        var now = Today.UtcDateTime;
        await using var db = _fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Title = "FIX10", Slug = $"fix10-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX10", Slug = $"fix10-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 1, BasePrice = 100m, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"FIX10-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, SubtotalAmount = 100m, FinalAmount = 100m, CurrencyType = 2, CreatedAt = now, PaidAt = now };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = "FIX10", Quantity = 1, UnitPrice = 100m, TotalPrice = 100m, CurrencyType = 2, DeliveryType = 1, DeliveryStatus = 1, RequiresVerification = true, KycRequirementMode = 1, KycPolicyVersionId = versionId, CreatedAt = now };
        db.AddRange(category, product, order, item, new OrderItemKycState { Id = Guid.NewGuid(), OrderItemId = item.Id, Status = (byte)OrderItemKycStatus.AwaitingSubmission, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        return item.Id;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
