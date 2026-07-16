using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class UploadIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public UploadIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Private_KYC_upload_uses_server_filename_and_is_not_publicly_served()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
        using var content = Multipart("../../unsafe-original.png", "image/png", png);

        var response = await client.PostAsync("/api/uploads/verification-document", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = (await response.Content.ReadFromJsonAsync<ApiResult<UploadFileResultDto>>())!.Data!;
        result.FileName.Should().MatchRegex("^[a-f0-9]{32}\\.png$");
        result.FilePath.Should().Be($"kyc-private:{user.Id:N}/{result.FileName}");

        using var anonymous = _fixture.CreateClient();
        (await anonymous.GetAsync($"/uploads/verifications/{result.FileName}")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);

        var environment = _fixture.Factory.Services.GetRequiredService<IWebHostEnvironment>();
        var stored = Path.Combine(environment.ContentRootPath, "private", "verification-documents", user.Id.ToString("N"), result.FileName);
        File.Exists(stored).Should().BeTrue();
        File.Delete(stored);
    }

    [Theory]
    [InlineData("payload.exe", "image/png")]
    [InlineData("payload.png", "application/octet-stream")]
    [InlineData("payload.png", "image/png")]
    public async Task Invalid_extension_MIME_or_signature_is_rejected(string fileName, string contentType)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart(fileName, contentType, "not an image"u8.ToArray());

        (await client.PostAsync("/api/uploads/verification-document", content)).StatusCode
            .Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Customer_cannot_use_admin_upload_endpoint()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        using var content = Multipart("image.png", "image/png", new byte[16]);
        (await client.PostAsync("/api/admin/uploads/product-image", content)).StatusCode
            .Should().Be(HttpStatusCode.Forbidden);
    }

    private static MultipartFormDataContent Multipart(string fileName, string contentType, byte[] bytes)
    {
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        multipart.Add(file, "file", fileName);
        return multipart;
    }
}
