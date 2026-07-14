using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Products;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

public sealed class ProductExperienceSecurityTests
{
    [Fact]
    public void Product_feature_requires_title_value_and_allowlisted_icon()
    {
        Assert.Throws<BusinessException>(() => ProductFeatureRules.Validate(new ProductFeatureDto { Title = "", Value = "PS5" }));
        Assert.Throws<BusinessException>(() => ProductFeatureRules.Validate(new ProductFeatureDto { Title = "پلتفرم", Value = "PS5", IconKey = "<script>" }));
        var valid = new ProductFeatureDto { Title = " پلتفرم ", Value = " PS5 ", IconKey = "gamepad" };
        ProductFeatureRules.Validate(valid);
        Assert.Equal("پلتفرم", valid.Title);
    }

    [Fact]
    public void Product_field_definition_rejects_unsafe_key_and_invalid_regex()
    {
        var unsafeKey = Field("../../secret");
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateDefinition(unsafeKey));
        var regex = Field("account_email"); regex.ValidationPattern = "[";
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateDefinition(regex));
    }

    [Fact]
    public void Select_requires_options_and_rejects_unknown_value()
    {
        var field = Field("platform"); field.FieldType = (byte)ProductInputFieldType.Select;
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateDefinition(field));
        field.Options = ["PS5", "Xbox"];
        ProductInputRules.ValidateDefinition(field);
        Assert.Equal("PS5", ProductInputRules.ValidateValue(field, "PS5"));
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateValue(field, "InternalAdminValue"));
    }

    [Fact]
    public void Required_field_and_type_specific_validation_are_enforced()
    {
        var email = Field("account_email"); email.FieldType = (byte)ProductInputFieldType.Email; email.IsRequired = true;
        ProductInputRules.ValidateDefinition(email);
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateValue(email, null));
        Assert.Throws<BusinessException>(() => ProductInputRules.ValidateValue(email, "not-an-email"));
        Assert.Equal("buyer@example.com", ProductInputRules.ValidateValue(email, " buyer@example.com "));
    }

    [Fact]
    public void Different_custom_values_produce_different_cart_identity()
    {
        var first = ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["platform_id"] = "A-100" });
        var second = ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["platform_id"] = "B-200" });
        Assert.NotEqual(first, second);
        Assert.Equal(first, ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["platform_id"] = "A-100" }));
    }

    [Fact]
    public void Sensitive_values_are_masked()
    {
        var masked = ProductInputRules.Mask("super-secret-value");
        Assert.DoesNotContain("super-secret-value", masked);
        Assert.StartsWith("su", masked);
        Assert.EndsWith("ue", masked);
    }

    [Theory]
    [InlineData("<script>alert(1)</script><p>safe</p>", "script")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>", "javascript")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>", "iframe")]
    public void Html_sanitizer_removes_xss_payloads(string html, string forbidden)
    {
        var sanitized = new StrictHtmlContentSanitizer().Sanitize(html)!;
        Assert.DoesNotContain(forbidden, sanitized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Html_sanitizer_preserves_document_formatting()
    {
        var sanitized = new StrictHtmlContentSanitizer().Sanitize("<h2>عنوان</h2><table><tbody><tr><td>مقدار</td></tr></tbody></table>");
        Assert.Contains("<h2>", sanitized);
        Assert.Contains("<table>", sanitized);
    }

    [Theory]
    [InlineData(".woff2", "font/woff2", "wOF2")]
    [InlineData(".woff", "font/woff", "wOFF")]
    public void Font_validator_accepts_matching_official_signatures(string extension, string mime, string signature)
    {
        Assert.Equal(extension[1..], FontFileValidator.Validate(extension, mime, 1024, System.Text.Encoding.ASCII.GetBytes(signature)));
    }

    [Fact]
    public void Font_validator_rejects_spoofed_or_oversized_files()
    {
        Assert.Throws<BusinessException>(() => FontFileValidator.Validate(".woff2", "font/woff2", 1024, "MZ!!"u8));
        Assert.Throws<BusinessException>(() => FontFileValidator.Validate(".woff2", "font/woff2", FontFileValidator.DefaultMaxBytes + 1, "wOF2"u8));
    }

    [Fact]
    public void Trust_seal_urls_are_provider_allowlisted()
    {
        TrustSealRules.ValidateSetting("TrustSeal.Enamad.Url", "https://trustseal.enamad.ir/verify?id=1");
        Assert.Throws<BusinessException>(() => TrustSealRules.ValidateSetting("TrustSeal.Enamad.Url", "https://evil.example/enamad"));
        Assert.Throws<BusinessException>(() => TrustSealRules.ValidateSetting("TrustSeal.Enamad.Url", "http://enamad.ir/verify"));
    }

    private static ProductInputFieldDto Field(string key) => new()
    {
        Key = key, Label = "شناسه حساب", FieldType = (byte)ProductInputFieldType.Text,
        DisplayStage = (byte)ProductInputStage.ProductPage, IsActive = true
    };
}
