using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Api.Hosting;
using Vitorize.Application.DTOs.Admin.Uploads;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>Low-level storage and upload-contract evidence for Phase 3A.</summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase3ARedactedUploadIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase3ARedactedUploadIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Valid_raster_uploads_are_server_named_and_failed_uploads_leave_no_orphans()
    {
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var client = _fixture.CreateClient(token);
        var root = _fixture.Factory.Services.GetRequiredService<HostingStoragePaths>().PrivateDocumentsRoot;
        var ownerDirectory = Path.Combine(root, user.Id.ToString("N"));
        try
        {
            var png = Png();
            var pngResult = await UploadAsync(client, "browser-flattened.png", "image/png", png, HttpStatusCode.OK);
            var stored = Path.Combine(ownerDirectory, pngResult.FileName);
            File.Exists(stored).Should().BeTrue();
            (await File.ReadAllBytesAsync(stored)).Should().Equal(png);
            Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(stored))).Should()
                .NotBe(Convert.ToHexString(SHA256.HashData(Jpeg())));

            (await UploadAsync(client, "normal.jpg", "image/jpeg", Jpeg(), HttpStatusCode.OK)).FileName.Should().EndWith(".jpg");
            (await UploadAsync(client, "normal.webp", "image/webp", Webp(), HttpStatusCode.OK)).FileName.Should().EndWith(".webp");

            var beforeFailed = Directory.GetFiles(ownerDirectory).Length;
            await UploadAsync(client, "document.pdf", "application/pdf", "%PDF-1.7"u8.ToArray(), HttpStatusCode.BadRequest);
            await UploadAsync(client, "document.svg", "image/svg+xml", "<svg/>"u8.ToArray(), HttpStatusCode.BadRequest);
            await UploadAsync(client, "spoofed.png", "image/png", "not-an-image"u8.ToArray(), HttpStatusCode.BadRequest);
            await UploadAsync(client, "oversized.png", "image/png", new byte[5 * 1024 * 1024 + 1], HttpStatusCode.BadRequest);
            Directory.GetFiles(ownerDirectory).Should().HaveCount(beforeFailed, "rejected files are deleted before the API returns");
        }
        finally
        {
            if (Directory.Exists(ownerDirectory)) Directory.Delete(ownerDirectory, true);
        }
    }

    [Fact]
    public async Task Stored_upload_preserves_document_type_and_blocks_other_customer_and_preview()
    {
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (other, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var now = DateTime.UtcNow;
        var type = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p3a-{Guid.NewGuid():N}", Title = "P3A document", IsActive = true, AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 5 * 1024 * 1024 };
        var category = new Category { Id = Guid.NewGuid(), Title = "P3A", Slug = $"p3a-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p3a-{Guid.NewGuid():N}", Name = "P3A", IsActive = true, CreatedAt = now };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "P3A", CreatedAt = now, PublishedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "P3A", Slug = $"p3a-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2, BasePrice = 1, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = now };
        var order = new Order { Id = Guid.NewGuid(), UserId = owner.Id, OrderNumber = $"P3A-{Guid.NewGuid():N}", Status = 2, PaymentStatus = 2, SubtotalAmount = 1, FinalAmount = 1, CurrencyType = 2, CreatedAt = now };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 1, TotalPrice = 1, CurrencyType = 2, DeliveryType = 2, DeliveryStatus = 1, RequiresVerification = true, KycRequirementMode = 1, KycPolicyVersionId = version.Id, CreatedAt = now };
        await using (var db = _fixture.CreateDbContext())
        {
            db.AddRange(type, category, policy, version, product, order, item,
                new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = version.Id, KycDocumentTypeId = type.Id, IsRequired = true },
                new UserVerificationProfile { Id = Guid.NewGuid(), UserId = owner.Id, FirstName = "Owner", LastName = "P3A", NationalCode = "1234567890", Status = 0, CreatedAt = now },
                new UserVerificationProfile { Id = Guid.NewGuid(), UserId = other.Id, FirstName = "Other", LastName = "P3A", NationalCode = "0987654321", Status = 0, CreatedAt = now });
            await db.SaveChangesAsync();
        }
        using var ownerClient = _fixture.CreateClient(ownerToken);
        using var otherClient = _fixture.CreateClient(otherToken);
        var upload = await UploadAsync(ownerClient, "flattened.png", "image/png", Png(), HttpStatusCode.OK);
        try
        {
            var add = await ownerClient.PostAsJsonAsync("/api/verification/documents", new { DocumentType = (byte)1, KycDocumentTypeId = type.Id, OrderItemId = item.Id, FilePath = upload.FilePath, IsRedacted = true });
            add.StatusCode.Should().Be(HttpStatusCode.OK);
            var document = (await add.Content.ReadFromJsonAsync<ApiResult<Vitorize.Application.DTOs.Verification.VerificationDocumentDto>>())!.Data!;
            document.KycDocumentTypeId.Should().Be(type.Id);
            (await ownerClient.GetAsync($"/api/verification/documents/{document.Id}/content")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await otherClient.GetAsync($"/api/verification/documents/{document.Id}/content")).StatusCode.Should().Be(HttpStatusCode.NotFound);

            var foreign = await otherClient.PostAsJsonAsync("/api/verification/documents", new { DocumentType = (byte)1, KycDocumentTypeId = type.Id, OrderItemId = item.Id, FilePath = upload.FilePath, IsRedacted = true });
            foreign.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            await using var verify = _fixture.CreateDbContext();
            (await verify.VerificationDocuments.CountAsync(x => x.KycDocumentTypeId == type.Id)).Should().Be(1);
        }
        finally
        {
            var root = _fixture.Factory.Services.GetRequiredService<HostingStoragePaths>().PrivateDocumentsRoot;
            var ownerDirectory = Path.Combine(root, owner.Id.ToString("N"));
            if (Directory.Exists(ownerDirectory)) Directory.Delete(ownerDirectory, true);
        }
    }

    [Fact]
    public async Task Required_policy_rejects_the_normal_upload_contract_but_cannot_cryptographically_attest_pixels()
    {
        var (owner, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var category = new Category { Id = Guid.NewGuid(), Title = "P3A", Slug = $"p3a-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p3a-required-{Guid.NewGuid():N}", Name = "P3A required", IsActive = true, CreatedAt = DateTime.UtcNow };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Required", CreatedAt = DateTime.UtcNow };
        var type = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p3a-required-doc-{Guid.NewGuid():N}", Title = "Required document", IsActive = true, AllowedExtensions = "jpg,jpeg,png,webp", MaxFileSizeBytes = 5 * 1024 * 1024 };
        var wrongType = new KycDocumentType { Id = Guid.NewGuid(), Code = $"p3a-wrong-doc-{Guid.NewGuid():N}", Title = "Wrong document", IsActive = true };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "P3A", Slug = $"p3a-product-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = 2, BasePrice = 1, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
        var order = new Order { Id = Guid.NewGuid(), UserId = owner.Id, OrderNumber = $"P3A-{Guid.NewGuid():N}", Status = 2, PaymentStatus = 2, SubtotalAmount = 1, FinalAmount = 1, CurrencyType = 2, CreatedAt = DateTime.UtcNow };
        var item = new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 1, TotalPrice = 1, CurrencyType = 2, DeliveryType = 2, DeliveryStatus = 1, RequiresVerification = true, KycRequirementMode = 1, KycPolicyVersionId = version.Id, CreatedAt = DateTime.UtcNow };
        await using (var db = _fixture.CreateDbContext())
        {
            db.AddRange(category, policy, version, type, wrongType, product, order, item,
                new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = version.Id, KycDocumentTypeId = type.Id, IsRequired = true, RedactionMode = 2 },
                new UserVerificationProfile { Id = Guid.NewGuid(), UserId = owner.Id, FirstName = "Required", LastName = "Owner", NationalCode = "1111111111", Status = 0, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = _fixture.CreateClient(token);
        var upload = await UploadAsync(client, "flattened.png", "image/png", Png(), HttpStatusCode.OK);
        try
        {
            var normal = await client.PostAsJsonAsync("/api/verification/documents", new { DocumentType = (byte)1, KycDocumentTypeId = type.Id, OrderItemId = item.Id, FilePath = upload.FilePath, IsRedacted = false });
            normal.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var wrongSlot = await client.PostAsJsonAsync("/api/verification/documents", new { DocumentType = (byte)2, KycDocumentTypeId = wrongType.Id, OrderItemId = item.Id, FilePath = upload.FilePath, IsRedacted = true });
            wrongSlot.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var redactedContract = await client.PostAsJsonAsync("/api/verification/documents", new { DocumentType = (byte)1, KycDocumentTypeId = type.Id, OrderItemId = item.Id, FilePath = upload.FilePath, IsRedacted = true });
            redactedContract.StatusCode.Should().Be(HttpStatusCode.OK);
        }
        finally
        {
            var root = _fixture.Factory.Services.GetRequiredService<HostingStoragePaths>().PrivateDocumentsRoot;
            var ownerDirectory = Path.Combine(root, owner.Id.ToString("N"));
            if (Directory.Exists(ownerDirectory)) Directory.Delete(ownerDirectory, true);
        }
    }

    private static async Task<UploadFileResultDto> UploadAsync(HttpClient client, string name, string mime, byte[] bytes, HttpStatusCode expected)
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(file, "file", name);
        var response = await client.PostAsync("/api/uploads/verification-document", form);
        response.StatusCode.Should().Be(expected);
        if (expected != HttpStatusCode.OK) return new UploadFileResultDto();
        return (await response.Content.ReadFromJsonAsync<ApiResult<UploadFileResultDto>>())!.Data!;
    }

    private static byte[] Png() => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
    // A valid tiny JPEG; upload validation deliberately verifies the raster signature, not client MIME alone.
    private static byte[] Jpeg() => Convert.FromBase64String("/9j/4AAQSkZJRgABAQEASABIAAD/2wBDAP//////////////////////////////////////////////////////////////////////////////////////2wBDAf//////////////////////////////////////////////////////////////////////////////////////wAARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAF//8QAFBABAAAAAAAAAAAAAAAAAAAAAP/aAAgBAQABBQL/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAEDAQE/Aaf/xAAUEQEAAAAAAAAAAAAAAAAAAAAA/9oACAECAQE/Aaf/xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAY/Ah//xAAUEAEAAAAAAAAAAAAAAAAAAAAA/9oACAEBAAE/IV//2gAMAwEAAgADAAAAEP/EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQMBAT8QH//EABQRAQAAAAAAAAAAAAAAAAAAABD/2gAIAQIBAT8QH//EABQQAQAAAAAAAAAAAAAAAAAAABD/2gAIAQEAAT8QH//Z");
    // RIFF/WEBP VP8 lossy one-pixel image (not a renamed PNG).
    private static byte[] Webp() => Convert.FromBase64String("UklGRiIAAABXRUJQVlA4IBYAAACQAgCdASoBAAEAAUAmJaQAA3AA/vuUAAA=");
}
