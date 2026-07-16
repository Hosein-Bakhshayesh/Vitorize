using System.Reflection;
using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.ProductVariants;
using Vitorize.Application.DTOs.Products;
using Vitorize.Application.DTOs.Wishlist;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Icons;
using Vitorize.Web.Services;
using Vitorize.Web.Services.UI;
using Xunit;

namespace Vitorize.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class BusinessRulesUnitTests
{
    public static TheoryData<decimal, decimal?, decimal> ProductPrices => new()
    {
        { 100_000m, 80_000m, 80_000m },
        { 100_000m, null, 100_000m },
        { 100_000m, 0m, 100_000m },
        { 100_000m, 100_000m, 100_000m },
        { 100_000m, 120_000m, 100_000m }
    };

    [Theory]
    [MemberData(nameof(ProductPrices))]
    public void Product_price_contract_accepts_only_positive_discount_below_base(
        decimal basePrice, decimal? discount, decimal expected)
    {
        new ProductDetailDto { BasePrice = basePrice, DiscountPrice = discount }.FinalPrice.Should().Be(expected);
        new ProductListItemDto { BasePrice = basePrice, DiscountPrice = discount }.FinalPrice.Should().Be(expected);
        new WishlistItemDto { BasePrice = basePrice, DiscountPrice = discount }.FinalPrice.Should().Be(expected);
        new AdminProductVariantDto { Price = basePrice, DiscountPrice = discount }.FinalPrice.Should().Be(expected);
    }

    [Theory]
    [MemberData(nameof(ProductPrices))]
    public void Cart_and_checkout_repricing_follow_the_same_discount_contract(
        decimal basePrice, decimal? discount, decimal expected)
    {
        InvokeDecimalRule(typeof(CartService), "ResolveFinalPrice", basePrice, discount).Should().Be(expected);
        InvokeDecimalRule(typeof(CheckoutService), "ResolveFinalPrice", basePrice, discount).Should().Be(expected);
    }

    [Theory]
    [InlineData(100_000, DiscountType.Percentage, 25, 25_000)]
    [InlineData(100_000, DiscountType.FixedAmount, 30_000, 30_000)]
    [InlineData(100_000, DiscountType.FixedAmount, 200_000, 100_000)]
    [InlineData(100_000, DiscountType.Percentage, 100, 100_000)]
    public void Coupon_calculation_supports_percentage_fixed_and_order_amount_cap(
        decimal orderAmount, DiscountType type, decimal value, decimal expected)
    {
        InvokeDecimalRule(typeof(CouponService), "CalculateDiscount", orderAmount, (byte)type, value)
            .Should().Be(expected);
    }

    [Fact]
    public void Product_feature_normalizes_content_and_legacy_icon_alias()
    {
        var feature = new ProductFeatureDto { Title = " Platform ", Value = " PS5 ", IconKey = "cart" };

        ProductFeatureRules.Validate(feature);

        feature.Title.Should().Be("Platform");
        feature.Value.Should().Be("PS5");
        feature.IconKey.Should().Be("shopping-cart");
    }

    [Theory]
    [InlineData("", "value")]
    [InlineData("title", "")]
    public void Product_feature_rejects_missing_title_or_value(string title, string value)
    {
        var action = () => ProductFeatureRules.Validate(new ProductFeatureDto { Title = title, Value = value });
        action.Should().Throw<BusinessException>();
    }

    [Theory]
    [InlineData(ProductInputFieldType.Email, "buyer@example.com", "buyer@example.com")]
    [InlineData(ProductInputFieldType.Mobile, "+98 912 345 6789", "09123456789")]
    [InlineData(ProductInputFieldType.Number, "1250.50", "1250.50")]
    [InlineData(ProductInputFieldType.TelegramUsername, "@vitorize_user", "@vitorize_user")]
    [InlineData(ProductInputFieldType.Url, "https://vitorize.com/account", "https://vitorize.com/account")]
    [InlineData(ProductInputFieldType.Date, "2026-07-16", "2026-07-16")]
    public void Dynamic_field_validates_and_normalizes_supported_types(
        ProductInputFieldType type, string value, string expected)
    {
        var field = new ProductInputFieldBuilder().WithType(type).Required().Build();
        ProductInputRules.ValidateDefinition(field);

        ProductInputRules.ValidateValue(field, value).Should().Be(expected);
    }

    [Theory]
    [InlineData(ProductInputFieldType.Email, "not-an-email")]
    [InlineData(ProductInputFieldType.Mobile, "02112345678")]
    [InlineData(ProductInputFieldType.Number, "one")]
    [InlineData(ProductInputFieldType.TelegramUsername, "@x")]
    [InlineData(ProductInputFieldType.Url, "javascript:alert(1)")]
    [InlineData(ProductInputFieldType.Date, "not-a-date")]
    public void Dynamic_field_rejects_invalid_type_specific_values(ProductInputFieldType type, string value)
    {
        var field = new ProductInputFieldBuilder().WithType(type).Build();
        ProductInputRules.ValidateDefinition(field);

        var action = () => ProductInputRules.ValidateValue(field, value);

        action.Should().Throw<BusinessException>();
    }

    [Theory]
    [InlineData("true", "true")]
    [InlineData("1", "true")]
    [InlineData("false", "false")]
    [InlineData("anything", "false")]
    public void Checkbox_values_have_a_canonical_representation(string input, string expected)
    {
        var field = new ProductInputFieldBuilder().WithType(ProductInputFieldType.Checkbox).Build();
        ProductInputRules.ValidateValue(field, input).Should().Be(expected);
    }

    [Fact]
    public void Required_checkbox_must_be_explicitly_confirmed()
    {
        var field = new ProductInputFieldBuilder().WithType(ProductInputFieldType.Checkbox).Required().Build();
        var action = () => ProductInputRules.ValidateValue(field, "false");
        action.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Select_options_are_trimmed_deduplicated_and_ordered_deterministically()
    {
        var field = new ProductInputFieldBuilder().WithType(ProductInputFieldType.Select)
            .WithOptions(" PS5 ", "Xbox", "PS5", "").Build();

        ProductInputRules.ValidateDefinition(field);

        field.Options.Should().Equal("PS5", "Xbox");
        ProductInputRules.ValidateValue(field, "PS5").Should().Be("PS5");
        ((Action)(() => ProductInputRules.ValidateValue(field, "PC"))).Should().Throw<BusinessException>();
    }

    [Fact]
    public void Secret_field_requires_sensitive_flag()
    {
        var invalid = new ProductInputFieldBuilder().WithType(ProductInputFieldType.Secret).Build();
        var valid = new ProductInputFieldBuilder().WithType(ProductInputFieldType.Secret).Sensitive().Build();

        ((Action)(() => ProductInputRules.ValidateDefinition(invalid))).Should().Throw<BusinessException>();
        ProductInputRules.ValidateDefinition(valid);
    }

    [Fact]
    public void Field_fingerprint_is_order_independent_but_value_sensitive()
    {
        var first = ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["b"] = "2", ["a"] = "1" });
        var reordered = ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "2" });
        var changed = ProductInputRules.Fingerprint(new Dictionary<string, string?> { ["a"] = "1", ["b"] = "3" });

        first.Should().Be(reordered).And.NotBe(changed);
        ProductInputRules.Fingerprint([]).Should().Be("NONE");
    }

    [Theory]
    [InlineData(null, "••••")]
    [InlineData("abc", "•••")]
    [InlineData("sensitive", "se••••ve")]
    public void Sensitive_dynamic_values_are_masked(string? value, string expected)
    {
        ProductInputRules.Mask(value).Should().Be(expected);
    }

    [Theory]
    [InlineData(".woff2", "font/woff2", "wOF2", "woff2")]
    [InlineData(".woff", "application/font-woff", "wOFF", "woff")]
    [InlineData(".ttf", "font/ttf", "true", "ttf")]
    public void Font_upload_accepts_only_matching_mime_extension_and_signature(
        string extension, string mime, string signature, string expected)
    {
        FontFileValidator.Validate(extension, mime, 100, System.Text.Encoding.ASCII.GetBytes(signature)).Should().Be(expected);
    }

    [Theory]
    [InlineData(".exe", "application/octet-stream", "MZ!!")]
    [InlineData(".woff2", "image/png", "wOF2")]
    [InlineData(".woff2", "font/woff2", "wOFF")]
    public void Font_upload_rejects_extension_mime_or_signature_spoofing(string extension, string mime, string signature)
    {
        var action = () => FontFileValidator.Validate(extension, mime, 100, System.Text.Encoding.ASCII.GetBytes(signature));
        action.Should().Throw<BusinessException>();
    }

    [Fact]
    public void Canonical_url_uses_configured_https_origin_and_strips_query_fragment_and_trailing_slash()
    {
        var branding = new StoreBranding(new Dictionary<string, string>
        {
            ["Seo.CanonicalBaseUrl"] = "https://www.vitorize.com/base?ignored=true"
        });

        SeoUrlBuilder.Canonical(branding, "http://localhost:5000/product/item?utm=x#section", "/product/item/?q=x")
            .Should().Be("https://www.vitorize.com/product/item");
    }

    [Fact]
    public void Json_ld_serialization_uses_camel_case_and_escapes_closing_script_sequence()
    {
        var json = SeoJsonLd.Serialize(new { ProductName = "</script><script>alert(1)</script>" });
        json.Should().Contain("productName").And.NotContain("</script>").And.NotContain("<script>");
        json.Should().Contain("\\u003C/script\\u003E");
    }

    [Fact]
    public void Breadcrumb_json_ld_has_one_based_positions()
    {
        var json = SeoJsonLd.Breadcrumbs(("Home", "https://vitorize.com/"), ("Product", "https://vitorize.com/p/x"));
        json.Should().Contain("BreadcrumbList").And.Contain("\"position\":1").And.Contain("\"position\":2");
    }

    [Theory]
    [InlineData("shopping_cart", "shopping-cart")]
    [InlineData(" Cart ", "shopping-cart")]
    [InlineData("CHECK-CIRCLE", "circle-check")]
    public void Lucide_keys_normalize_official_and_legacy_aliases(string input, string expected)
    {
        LucideIconCatalog.TryNormalizeKey(input, out var normalized).Should().BeTrue();
        normalized.Should().Be(expected);
    }

    [Fact]
    public void Lucide_search_respects_category_exclusion_limit_and_stable_order()
    {
        var results = LucideIconCatalog.Search("wallet", "Wallet", 5, ["wallet"]);
        results.Should().NotBeEmpty().And.HaveCountLessOrEqualTo(5);
        results.Should().OnlyContain(x => x.Category == "Wallet" && x.Key != "wallet");
        LucideIconCatalog.Search("wallet", "Wallet", 5, ["wallet"]).Select(x => x.Key)
            .Should().Equal(results.Select(x => x.Key));
    }

    [Fact]
    public void Lucide_invalid_key_falls_back_to_an_official_icon()
    {
        LucideIconCatalog.ResolveOrFallback("<script>", "not-real").Should().Be("circle");
        ((Func<string?>)(() => LucideIconRules.NormalizeOptional("<script>")))
            .Should().Throw<BusinessException>();
    }

    private static decimal InvokeDecimalRule(Type service, string method, params object?[] arguments)
    {
        var target = service.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static);
        target.Should().NotBeNull($"{service.Name}.{method} is a critical pricing rule");
        return (decimal)target!.Invoke(null, arguments)!;
    }
}
