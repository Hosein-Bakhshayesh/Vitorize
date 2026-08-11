using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase1AuthorizationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase1AuthorizationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Kyc_view_and_manage_permissions_are_enforced_for_real_http_actors()
    {
        var (_, manageToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        var (_, viewToken) = await _fixture.CreateUserAndTokenAsync("KycViewer");
        await using (var db = _fixture.CreateDbContext())
        {
            if (!await db.Roles.AnyAsync(x => x.Name == "KycNoAccessAdmin"))
                db.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "KycNoAccessAdmin", DisplayName = "No KYC access", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        var (_, noAccessToken) = await _fixture.CreateUserAndTokenAsync("KycNoAccessAdmin");
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var manage = _fixture.CreateClient(manageToken);
        using var viewer = _fixture.CreateClient(viewToken);
        using var noAccess = _fixture.CreateClient(noAccessToken);
        using var customer = _fixture.CreateClient(customerToken);
        using var anonymous = _fixture.CreateClient();

        var reads = new[] { "/api/admin/kyc/policies", "/api/admin/kyc/policy-versions", "/api/admin/kyc/document-types" };
        foreach (var path in reads)
        {
            (await manage.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await viewer.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.OK);
            (await noAccess.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await customer.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await anonymous.GetAsync(path)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        var code = $"auth-{Guid.NewGuid():N}";
        var request = new UpsertKycPolicyRequestDto { Code = code, Name = "Authorization policy", CustomerTitle = "Authorization V1", IsActive = true };
        (await viewer.PostAsJsonAsync("/api/admin/kyc/policies", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await noAccess.PostAsJsonAsync("/api/admin/kyc/policies", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync("/api/admin/kyc/policies", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await anonymous.PostAsJsonAsync("/api/admin/kyc/policies", request)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var createdResponse = await manage.PostAsJsonAsync("/api/admin/kyc/policies", request);
        createdResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = (await createdResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var v1 = policy.Versions.Single();
        async Task Deny(Func<HttpClient, Task<HttpResponseMessage>> call)
        {
            (await call(viewer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await call(noAccess)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await call(customer)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await call(anonymous)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        await Deny(c => c.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto { CustomerTitle = "V2" }));
        var v2Response = await manage.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto { CustomerTitle = "V2" });
        v2Response.StatusCode.Should().Be(HttpStatusCode.OK);
        var v2 = (await v2Response.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;

        await Deny(c => c.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}", new UpdateKycPolicyVersionRequestDto { CustomerTitle = "Updated", CustomerInstructions = "Updated" }));
        (await manage.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}", new UpdateKycPolicyVersionRequestDto { CustomerTitle = "Updated", CustomerInstructions = "Updated" })).StatusCode.Should().Be(HttpStatusCode.OK);

        var documentRequest = new UpsertKycDocumentTypeRequestDto { Code = $"doc-{Guid.NewGuid():N}", Title = "Authorization document", IsActive = true, AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 1024 };
        await Deny(c => c.PostAsJsonAsync("/api/admin/kyc/document-types", documentRequest));
        var documentResponse = await manage.PostAsJsonAsync("/api/admin/kyc/document-types", documentRequest);
        documentResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var document = (await documentResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycDocumentTypeDto>>())!.Data!;
        await Deny(c => c.PutAsJsonAsync($"/api/admin/kyc/document-types/{document.Id}", documentRequest));
        (await manage.PutAsJsonAsync($"/api/admin/kyc/document-types/{document.Id}", documentRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

        var requirements = new SetKycPolicyDocumentRequirementsRequestDto { Requirements = [new KycPolicyDocumentRequirementRequestDto { KycDocumentTypeId = document.Id, IsRequired = true, SortOrder = 1 }] };
        await Deny(c => c.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/document-requirements", requirements));
        (await manage.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/document-requirements", requirements)).StatusCode.Should().Be(HttpStatusCode.OK);
        await Deny(c => c.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/publish", new { }));
        (await manage.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/publish", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task KycViewer_seed_is_idempotent_and_remains_review_only()
    {
        async Task<int> CountAsync()
        {
            await using var db = _fixture.CreateDbContext();
            return await db.Roles.CountAsync(x => x.Name == "KycViewer");
        }
        async Task SeedAsync()
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IVitorizeSeedService>().SeedAsync();
        }

        await SeedAsync();
        (await CountAsync()).Should().Be(1);
        AdminPermissions.ForRoles(["KycViewer"]).Should().Contain(AdminPermissions.KycReview).And.NotContain(AdminPermissions.KycManage);
        AdminPermissions.ForRoles(["Admin"]).Should().Contain([AdminPermissions.KycReview, AdminPermissions.KycManage]);
        await SeedAsync();
        (await CountAsync()).Should().Be(1);
        AdminPermissions.ForRoles(["KycViewer"]).Should().Contain(AdminPermissions.KycReview).And.NotContain(AdminPermissions.KycManage);
    }
}
