using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Vitorize.Application.DTOs.Admin.Kyc;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase3ARedactionConfigurationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase3ARedactionConfigurationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Draft_configuration_is_versioned_and_published_configuration_is_immutable()
    {
        var (_, managerToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        using var manager = _fixture.CreateClient(managerToken);
        var document = await CreateDocumentAsync(manager);
        var policyResponse = await manager.PostAsJsonAsync("/api/admin/kyc/policies", new UpsertKycPolicyRequestDto
        {
            Code = $"redaction-{Guid.NewGuid():N}", Name = "Redaction policy", CustomerTitle = "V1", IsActive = true
        });
        policyResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var policy = (await policyResponse.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var v1 = policy.Versions.Single();
        var required = new KycPolicyDocumentRequirementRequestDto
        {
            KycDocumentTypeId = document.Id, IsRequired = true, SortOrder = 1,
            RedactionMode = (byte)KycDocumentRedactionMode.Required,
            RedactionInstructions = "Keep the national ID number readable."
        };
        (await manager.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto { Requirements = [required] })).StatusCode.Should().Be(HttpStatusCode.OK);

        var read = await manager.GetFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>($"/api/admin/kyc/policy-versions/{v1.Id}");
        read!.Data!.DocumentRequirements.Should().ContainSingle().Which.Should().Match<AdminKycPolicyDocumentRequirementDto>(x =>
            x.RedactionMode == (byte)KycDocumentRedactionMode.Required && x.RedactionInstructions == required.RedactionInstructions);

        (await manager.PostAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/publish", new { })).StatusCode.Should().Be(HttpStatusCode.OK);
        required.RedactionMode = (byte)KycDocumentRedactionMode.None;
        (await manager.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v1.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto { Requirements = [required] })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var v2Response = await manager.PostAsJsonAsync($"/api/admin/kyc/policies/{policy.Id}/versions", new CreateKycPolicyVersionRequestDto { CustomerTitle = "V2" });
        var v2 = (await v2Response.Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>())!.Data!;
        (await manager.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{v2.Id}/document-requirements", new SetKycPolicyDocumentRequirementsRequestDto { Requirements = [required] })).StatusCode.Should().Be(HttpStatusCode.OK);
        var historical = await manager.GetFromJsonAsync<ApiResult<AdminKycPolicyVersionOptionDto>>($"/api/admin/kyc/policy-versions/{v1.Id}");
        historical!.Data!.DocumentRequirements.Single().RedactionMode.Should().Be((byte)KycDocumentRedactionMode.Required);
    }

    [Fact]
    public async Task Kyc_viewer_cannot_mutate_redaction_configuration()
    {
        var (_, managerToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        var (_, viewerToken) = await _fixture.CreateUserAndTokenAsync("KycViewer");
        using var manager = _fixture.CreateClient(managerToken);
        using var viewer = _fixture.CreateClient(viewerToken);
        var document = await CreateDocumentAsync(manager);
        var policy = (await (await manager.PostAsJsonAsync("/api/admin/kyc/policies", new UpsertKycPolicyRequestDto { Code = $"redaction-auth-{Guid.NewGuid():N}", Name = "Policy", CustomerTitle = "V1", IsActive = true })).Content.ReadFromJsonAsync<ApiResult<AdminKycPolicyDto>>())!.Data!;
        var request = new SetKycPolicyDocumentRequirementsRequestDto { Requirements = [new() { KycDocumentTypeId = document.Id, RedactionMode = (byte)KycDocumentRedactionMode.Optional }] };
        (await viewer.PutAsJsonAsync($"/api/admin/kyc/policy-versions/{policy.Versions.Single().Id}/document-requirements", request)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private static async Task<AdminKycDocumentTypeDto> CreateDocumentAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/admin/kyc/document-types", new UpsertKycDocumentTypeRequestDto
        {
            Code = $"redaction-document-{Guid.NewGuid():N}", Title = "Redaction document", IsActive = true,
            AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 5 * 1024 * 1024
        });
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResult<AdminKycDocumentTypeDto>>())!.Data!;
    }
}
