using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Coupons;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-13 (Client Issue #11) end to end over real HTTP and SQL Server: both calculation modes, the
/// preserved zero-payable invariant, wallet/gateway amounts and the KYC threshold regression.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix13VatCheckoutIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public Fix13VatCheckoutIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Vat_disabled_leaves_the_order_totals_exactly_as_before()
    {
        await SetVatAsync(enabled: false);
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000m);

        var checkout = await CheckoutAsync(token, product.Id, quantity: 2);

        checkout.SubtotalAmount.Should().Be(2_000m);
        checkout.DiscountAmount.Should().Be(0m);
        checkout.FinalAmount.Should().Be(2_000m);
        checkout.VatEnabled.Should().BeFalse();
        checkout.VatAmount.Should().Be(0m);
        checkout.VatRatePercent.Should().Be(0m);

        await using var db = _fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(x => x.Id == checkout.OrderId);
        order.UserId.Should().Be(user.Id);
        order.VatEnabled.Should().BeFalse();
        order.VatAmount.Should().Be(0m);
        order.VatTaxableAmount.Should().Be(0m);
        (await db.Payments.SingleAsync(x => x.OrderId == order.Id)).Amount.Should().Be(2_000m);
    }

    [Theory]
    // mode, rate, coupon (null = none), discountType, discountValue, subtotal, expectedDiscount, expectedTaxable, expectedVat, expectedFinal
    [InlineData(VatCalculationMode.BeforeDiscount, 10, false, 0, 0, 1_000_000, 0, 1_000_000, 100_000, 1_100_000)]
    [InlineData(VatCalculationMode.BeforeDiscount, 10, true, (byte)DiscountType.Percentage, 10, 1_000_000, 100_000, 1_000_000, 100_000, 1_000_000)]
    [InlineData(VatCalculationMode.BeforeDiscount, 9, true, (byte)DiscountType.FixedAmount, 120_000, 500_000, 120_000, 500_000, 45_000, 425_000)]
    [InlineData(VatCalculationMode.AfterDiscount, 10, false, 0, 0, 1_000_000, 0, 1_000_000, 100_000, 1_100_000)]
    [InlineData(VatCalculationMode.AfterDiscount, 10, true, (byte)DiscountType.Percentage, 10, 1_000_000, 100_000, 900_000, 90_000, 990_000)]
    [InlineData(VatCalculationMode.AfterDiscount, 9, true, (byte)DiscountType.FixedAmount, 120_000, 500_000, 120_000, 380_000, 34_200, 414_200)]
    public async Task Both_modes_persist_the_approved_decomposition(
        VatCalculationMode mode, decimal rate, bool withCoupon, byte discountType, decimal discountValue,
        decimal subtotal, decimal expectedDiscount, decimal expectedTaxable, decimal expectedVat, decimal expectedFinal)
    {
        await SetVatAsync(enabled: true, rate, mode);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: subtotal);
        var couponCode = withCoupon ? await CreateCouponAsync(discountType, discountValue) : null;

        var checkout = await CheckoutAsync(token, product.Id, quantity: 1, couponCode);

        checkout.SubtotalAmount.Should().Be(subtotal);
        checkout.DiscountAmount.Should().Be(expectedDiscount);
        checkout.VatTaxableAmount.Should().Be(expectedTaxable);
        checkout.VatAmount.Should().Be(expectedVat);
        checkout.FinalAmount.Should().Be(expectedFinal);

        await using var db = _fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(x => x.Id == checkout.OrderId);
        order.VatEnabled.Should().BeTrue();
        order.VatRatePercent.Should().Be(rate);
        order.VatCalculationMode.Should().Be((byte)mode);
        order.VatTaxableAmount.Should().Be(expectedTaxable);
        order.VatAmount.Should().Be(expectedVat);
        order.FinalAmount.Should().Be(expectedFinal);

        // Persisted reconciliation identity.
        if (mode == VatCalculationMode.BeforeDiscount)
            (order.SubtotalAmount + order.VatAmount - order.DiscountAmount).Should().Be(order.FinalAmount);
        else
            (order.VatTaxableAmount + order.VatAmount).Should().Be(order.FinalAmount);

        (await db.Payments.SingleAsync(x => x.OrderId == order.Id)).Amount.Should().Be(order.FinalAmount);
    }

    [Theory]
    [InlineData(VatCalculationMode.BeforeDiscount, (byte)DiscountType.Percentage, 100)]
    [InlineData(VatCalculationMode.AfterDiscount, (byte)DiscountType.Percentage, 100)]
    [InlineData(VatCalculationMode.BeforeDiscount, (byte)DiscountType.FixedAmount, 1_000)]
    [InlineData(VatCalculationMode.AfterDiscount, (byte)DiscountType.FixedAmount, 2_000)]
    public async Task A_fully_discounted_basket_stays_unsupported_and_never_becomes_a_tax_only_order(
        VatCalculationMode mode, byte discountType, decimal discountValue)
    {
        await SetVatAsync(enabled: true, 10m, mode);
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000m);
        var couponCode = await CreateCouponAsync(discountType, discountValue);

        using var client = _fixture.CreateClient(token);
        (await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 })).EnsureSuccessStatusCode();
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-zero-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());
        await using var db = _fixture.CreateDbContext();
        (await db.Orders.CountAsync(x => x.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Coupon_preview_matches_the_amount_the_order_is_created_with()
    {
        await SetVatAsync(enabled: true, 10m, VatCalculationMode.AfterDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000_000m);
        var couponCode = await CreateCouponAsync((byte)DiscountType.Percentage, 10m);
        using var client = _fixture.CreateClient(token);

        (await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 })).EnsureSuccessStatusCode();

        // The cart preview carries a no-coupon VAT decomposition.
        var cart = (await (await client.GetAsync("/api/cart")).Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        cart.VatEnabled.Should().BeTrue();
        cart.VatRatePercent.Should().Be(10m);
        cart.DiscountAmount.Should().Be(0m);
        cart.VatAmount.Should().Be(100_000m);
        cart.FinalAmount.Should().Be(1_100_000m);

        var previewResponse = await client.PostAsJsonAsync("/api/coupons/validate",
            new ValidateCouponRequestDto { Code = couponCode, OrderAmount = cart.SubtotalAmount });
        previewResponse.StatusCode.Should().Be(HttpStatusCode.OK, await previewResponse.Content.ReadAsStringAsync());
        var preview = (await previewResponse.Content.ReadFromJsonAsync<ApiResult<ValidateCouponResultDto>>())!.Data!;
        preview.VatEnabled.Should().BeTrue();
        preview.VatTaxableAmount.Should().Be(900_000m);
        preview.VatAmount.Should().Be(90_000m);
        preview.FinalAmount.Should().Be(990_000m);

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-parity-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, await checkoutResponse.Content.ReadAsStringAsync());
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;

        checkout.FinalAmount.Should().Be(preview.FinalAmount, "the preview must not drift from the authoritative calculation");
        checkout.VatAmount.Should().Be(preview.VatAmount);
        checkout.VatTaxableAmount.Should().Be(preview.VatTaxableAmount);
    }

    [Fact]
    public async Task A_client_cannot_inject_vat_values_through_the_checkout_request()
    {
        await SetVatAsync(enabled: true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000m);
        using var client = _fixture.CreateClient(token);

        (await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 })).EnsureSuccessStatusCode();

        // Hostile payload carrying every VAT field plus a forged total. All are ignored: the request
        // contract has no VAT properties and the server recalculates from its own configuration.
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-hostile-{Guid.NewGuid():N}");
        using var content = new StringContent(
            "{\"vatEnabled\":false,\"vatRatePercent\":0,\"vatAmount\":0,\"vatCalculationMode\":2," +
            "\"finalAmount\":1,\"subtotalAmount\":1,\"discountAmount\":999999}",
            System.Text.Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/checkout", content);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var checkout = (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;

        checkout.VatEnabled.Should().BeTrue("the server owns the VAT configuration");
        checkout.VatRatePercent.Should().Be(10m);
        checkout.VatCalculationMode.Should().Be((byte)VatCalculationMode.BeforeDiscount);
        checkout.VatAmount.Should().Be(100m);
        checkout.DiscountAmount.Should().Be(0m);
        checkout.FinalAmount.Should().Be(1_100m);
    }

    [Fact]
    public async Task Wallet_validates_and_debits_the_vat_inclusive_final_amount()
    {
        await SetVatAsync(enabled: true, 10m, VatCalculationMode.AfterDiscount);
        var (user, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000m);
        var couponCode = await CreateCouponAsync((byte)DiscountType.Percentage, 10m);

        // Subtotal 1,000 - 10% = 900 taxable, +90 VAT = 990 payable.
        var checkout = await CheckoutAsync(token, product.Id, quantity: 1, couponCode);
        checkout.FinalAmount.Should().Be(990m);

        // A balance covering the pre-VAT amount but not the VAT-inclusive total must be refused.
        await SetWalletBalanceAsync(user.Id, 900m);
        using var insufficientClient = _fixture.CreateClient(token);
        insufficientClient.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-wallet-low-{Guid.NewGuid():N}");
        var insufficient = await insufficientClient.PostAsync($"/api/payments/wallet/pay/{checkout.OrderId}", null);
        insufficient.StatusCode.Should().Be(HttpStatusCode.BadRequest, await insufficient.Content.ReadAsStringAsync());
        // Assert on the decoded message: the raw JSON escapes non-ASCII as \uXXXX.
        var insufficientResult = await insufficient.Content.ReadFromJsonAsync<ApiResult>();
        insufficientResult!.Message.Should().Contain("موجودی کیف پول کافی نیست",
            "the refusal must come from the balance check, not from a missing header");

        await SetWalletBalanceAsync(user.Id, 990m);
        using var client = _fixture.CreateClient(token);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-wallet-ok-{Guid.NewGuid():N}");
        var paid = await client.PostAsync($"/api/payments/wallet/pay/{checkout.OrderId}", null);
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync());

        await using var db = _fixture.CreateDbContext();
        (await db.Wallets.SingleAsync(x => x.UserId == user.Id)).Balance.Should().Be(0m);
        var order = await db.Orders.SingleAsync(x => x.Id == checkout.OrderId);
        order.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        var walletPayment = await db.Payments.SingleAsync(x => x.OrderId == order.Id && x.Gateway == "Wallet");
        walletPayment.Amount.Should().Be(order.FinalAmount);
    }

    [Fact]
    public async Task Gateway_payment_uses_the_persisted_final_amount_and_verifies_against_it()
    {
        await SetVatAsync(enabled: true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await CreateProductAsync(price: 1_000m);
        var couponCode = await CreateCouponAsync((byte)DiscountType.FixedAmount, 200m);

        // Subtotal 1,000 + 100 VAT - 200 discount = 900 payable.
        var checkout = await CheckoutAsync(token, product.Id, quantity: 1, couponCode);
        checkout.VatAmount.Should().Be(100m);
        checkout.FinalAmount.Should().Be(900m);

        using var client = _fixture.CreateClient(token);
        var start = await client.PostAsync($"/api/payments/start/{checkout.OrderId}", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());

        await using (var db = _fixture.CreateDbContext())
        {
            var attempts = await db.Payments.Where(x => x.OrderId == checkout.OrderId).ToListAsync();
            attempts.Should().OnlyContain(x => x.Amount == 900m, "the gateway is charged the persisted order amount");
        }

        var paymentId = await LatestPendingPaymentIdAsync(checkout.OrderId);
        var verify = await client.PostAsync($"/api/payments/mock/verify/{paymentId}", null);
        verify.StatusCode.Should().Be(HttpStatusCode.OK, await verify.Content.ReadAsStringAsync());

        await using var verified = _fixture.CreateDbContext();
        var order = await verified.Orders.SingleAsync(x => x.Id == checkout.OrderId);
        order.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verified.Payments.SingleAsync(x => x.Id == paymentId)).Amount.Should().Be(order.FinalAmount);
    }

    [Fact]
    public async Task Vat_is_included_in_the_final_order_total_kyc_evaluation()
    {
        await SetVatAsync(enabled: true, 10m, VatCalculationMode.BeforeDiscount);
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        // The configured threshold sits between the pre-VAT and final payable totals.
        // The order-total rule deliberately uses the amount the customer actually pays.
        await _fixture.ConfigureOrderTotalKycAsync(1_050m);
        var product = await CreateProductAsync(price: 1_000m, kycThreshold: 1_050m);

        var checkout = await CheckoutAsync(token, product.Id, quantity: 1);
        checkout.FinalAmount.Should().Be(1_100m);

        await using var db = _fixture.CreateDbContext();
        var item = await db.OrderItems.SingleAsync(x => x.OrderId == checkout.OrderId);
        item.KycEvaluatedAmount.Should().Be(1_100m, "KYC evaluates the final VAT-inclusive payable total");
        item.RequiresVerification.Should().BeTrue("the final 1,100 payable amount reaches the 1,050 threshold");
    }

    private async Task<CheckoutResultDto> CheckoutAsync(string token, Guid productId, int quantity, string? couponCode = null)
    {
        using var client = _fixture.CreateClient(token);
        var added = await client.PostAsJsonAsync("/api/cart/items",
            new AddToCartRequestDto { ProductId = productId, Quantity = quantity });
        added.StatusCode.Should().Be(HttpStatusCode.OK, await added.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix13-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
    }

    private async Task<Guid> LatestPendingPaymentIdAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Payments.Where(x => x.OrderId == orderId && x.Status == (byte)PaymentStatus.Pending)
            .OrderByDescending(x => x.RequestedAt).Select(x => x.Id).FirstAsync();
    }

    private async Task SetWalletBalanceAsync(Guid userId, decimal balance)
    {
        await using var db = _fixture.CreateDbContext();
        var wallet = await db.Wallets.SingleOrDefaultAsync(x => x.UserId == userId);
        if (wallet is null)
        {
            wallet = new Wallet { Id = Guid.NewGuid(), UserId = userId, CreatedAt = DateTime.UtcNow };
            db.Wallets.Add(wallet);
        }
        wallet.Balance = balance;
        await db.SaveChangesAsync();
    }

    internal async Task SetVatAsync(bool enabled, decimal ratePercent = 0m,
        VatCalculationMode mode = VatCalculationMode.BeforeDiscount)
    {
        await using var db = _fixture.CreateDbContext();
        await UpsertAsync(db, VatSettings.Keys.Enabled, enabled ? "true" : "false", "bool");
        await UpsertAsync(db, VatSettings.Keys.RatePercent,
            ratePercent.ToString(System.Globalization.CultureInfo.InvariantCulture), "decimal");
        await UpsertAsync(db, VatSettings.Keys.CalculationMode, VatSettings.ToSettingValue(mode), "vatmode");
        await db.SaveChangesAsync();
    }

    private static async Task UpsertAsync(Vitorize.Infrastructure.Persistence.VitorizeDbContext db,
        string key, string value, string valueType)
    {
        var setting = await db.Settings.SingleOrDefaultAsync(x => x.Key == key);
        if (setting is null)
        {
            setting = new Setting { Id = Guid.NewGuid(), Key = key, GroupName = VatSettings.Group, ValueType = valueType };
            db.Settings.Add(setting);
        }
        setting.Value = value;
        setting.UpdatedAt = DateTime.UtcNow;
    }

    internal async Task<string> CreateCouponAsync(byte discountType, decimal discountValue)
    {
        await using var db = _fixture.CreateDbContext();
        var code = $"FIX13{Guid.NewGuid():N}"[..12].ToUpperInvariant();
        db.Coupons.Add(new Coupon
        {
            Id = Guid.NewGuid(), Code = code, Title = "FIX-13 coupon",
            DiscountType = discountType, DiscountValue = discountValue,
            IsActive = true, CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return code;
    }

    internal async Task<Product> CreateProductAsync(decimal price, decimal? kycThreshold = null)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "FIX-13 category", Slug = $"fix13-{Guid.NewGuid():N}",
            SortOrder = 0, IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-13 product",
            Slug = $"fix13-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other,
            DeliveryType = (byte)DeliveryType.Manual, BasePrice = price,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        if (kycThreshold.HasValue)
        {
            var policyVersionId = await db.KycPolicyVersions.Select(x => x.Id).FirstAsync();
            product.RequiresVerification = true;
            product.KycRequirementMode = (byte)KycRequirementMode.AboveThreshold;
            product.KycThresholdAmount = kycThreshold.Value;
            product.KycPolicyVersionId = policyVersionId;
        }
        product.WithCanonicalVariant();
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }
}
