using System.Security.Cryptography;
using System.Text;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Validators.Admin;
using Vitorize.Application.Validators.Payments;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class FinancialSecurityHardeningTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void Encryption_uses_authenticated_v2_format_and_detects_tampering()
    {
        var sut = new AesEncryptionService(Options.Create(new EncryptionSettings { Key = Key }));
        var encrypted = sut.Encrypt("sensitive-gift-code");
        Assert.StartsWith("v2.", encrypted);
        Assert.DoesNotContain("sensitive-gift-code", encrypted);
        Assert.Equal("sensitive-gift-code", sut.Decrypt(encrypted));

        var parts = encrypted.Split('.');
        var cipher = Convert.FromBase64String(parts[3]);
        cipher[0] ^= 1;
        parts[3] = Convert.ToBase64String(cipher);
        Assert.ThrowsAny<Exception>(() => sut.Decrypt(string.Join('.', parts)));
    }

    [Fact]
    public void Encryption_reads_legacy_cbc_rows_for_safe_rotation()
    {
        var sut = new AesEncryptionService(Options.Create(new EncryptionSettings { Key = Key }));
        Assert.Equal("legacy-value", sut.Decrypt(CreateLegacyCipher("legacy-value")));
    }

    [Fact]
    public void Refund_and_manual_delivery_validation_rejects_unsafe_requests()
    {
        var refund = new PaymentRefundRequestValidator().TestValidate(new PaymentRefundRequestDto
        {
            Method = 99, Reason = "", IdempotencyKey = "contains spaces"
        });
        refund.ShouldHaveValidationErrorFor(x => x.Method);
        refund.ShouldHaveValidationErrorFor(x => x.Reason);
        refund.ShouldHaveValidationErrorFor(x => x.IdempotencyKey);

        var delivery = new ManualDeliveryRequestValidator().TestValidate(new ManualDeliveryRequestDto());
        delivery.ShouldHaveValidationErrorFor(x => x.OrderItemId);
        delivery.ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Ef_model_contains_financial_uniqueness_and_private_data_columns()
    {
        using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var paymentIndexes = db.Model.FindEntityType("Vitorize.Domain.Entities.Payment")!.GetIndexes();
        Assert.Contains(paymentIndexes, x => x.IsUnique &&
            x.Properties.Select(p => p.Name).SequenceEqual(new[] { "Gateway", "Authority" }));
        var callbackIndexes = db.Model.FindEntityType("Vitorize.Domain.Entities.PaymentCallback")!.GetIndexes();
        Assert.Contains(callbackIndexes, x => x.IsUnique && x.Properties.Any(p => p.Name == "CallbackKey"));
        Assert.NotNull(db.Model.FindEntityType("Vitorize.Domain.Entities.PaymentRefund"));
        Assert.NotNull(db.Model.FindEntityType("Vitorize.Domain.Entities.FinancialAuditLog"));
        Assert.NotNull(db.Model.FindEntityType("Vitorize.Domain.Entities.UserVerificationProfile")!
            .FindProperty("EncryptedPayload"));
    }

    [Fact]
    public void Permissions_are_least_privilege_by_role()
    {
        Assert.Equal(AdminPermissions.All.Order(), AdminPermissions.ForRoles(["SuperAdmin"]).Order());
        Assert.DoesNotContain(AdminPermissions.FinanceManage, AdminPermissions.ForRoles(["Admin"]));
        Assert.Contains(AdminPermissions.KycReview, AdminPermissions.ForRoles(["Admin"]));
        Assert.Equal([AdminPermissions.OrderFulfillment], AdminPermissions.ForRoles(["Support"]));
    }

    [Fact]
    public void Security_header_policy_blocks_framing_content_sniffing_and_unsafe_api_sources()
    {
        Assert.Equal("nosniff", SecurityHeaderPolicy.ContentTypeOptions);
        Assert.Equal("DENY", SecurityHeaderPolicy.ApiFrameOptions);
        Assert.Contains("default-src 'none'", SecurityHeaderPolicy.ApiContentSecurityPolicy);
        Assert.Contains("frame-ancestors 'none'", SecurityHeaderPolicy.ApiContentSecurityPolicy);
        Assert.Contains("object-src 'none'", SecurityHeaderPolicy.WebContentSecurityPolicy);
        Assert.Contains("upgrade-insecure-requests", SecurityHeaderPolicy.WebContentSecurityPolicy);
    }

    [Fact]
    public void Web_csp_only_allows_an_explicit_http_media_origin_for_loopback_testing()
    {
        var testing = SecurityHeaderPolicy.BuildWebContentSecurityPolicy("http://127.0.0.1:5177/uploads");
        var unsafePublicHttp = SecurityHeaderPolicy.BuildWebContentSecurityPolicy("http://media.example.com");

        Assert.Contains("img-src 'self' data: https: http://127.0.0.1:5177;", testing);
        Assert.Equal(SecurityHeaderPolicy.WebContentSecurityPolicy, unsafePublicHttp);
    }

    private static string CreateLegacyCipher(string value)
    {
        using var aes = Aes.Create();
        aes.Key = Encoding.UTF8.GetBytes(Key);
        aes.GenerateIV();
        using var encryptor = aes.CreateEncryptor();
        var plain = Encoding.UTF8.GetBytes(value);
        var cipher = encryptor.TransformFinalBlock(plain, 0, plain.Length);
        return Convert.ToBase64String(aes.IV.Concat(cipher).ToArray());
    }
}
