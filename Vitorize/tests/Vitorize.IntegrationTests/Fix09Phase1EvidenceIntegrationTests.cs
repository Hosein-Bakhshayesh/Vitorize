using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Admin.Products;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// SQL Server evidence for FIX-09 Phase 1. The requests enter the real cart and
/// checkout pipeline; the assertions read the persisted OrderItem snapshots.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase1EvidenceIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase1EvidenceIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Checkout_persists_order_total_threshold_quantity_coupon_and_mixed_cart_snapshots()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerifiedAsync(customer.Id);
        var policyVersionId = await _fixture.ConfigureOrderTotalKycAsync(5_000m);
        var below = await CreateProductAsync("below", 4_999m, KycRequirementMode.None, null, null);
        var equal = await CreateProductAsync("equal", 5_000m, KycRequirementMode.None, null, null);
        var above = await CreateProductAsync("above", 5_001m, KycRequirementMode.None, null, null);
        var quantity = await CreateProductAsync("quantity", 2_500m, KycRequirementMode.None, null, null);
        var none = await CreateProductAsync("none", 250m, KycRequirementMode.None, null, null);
        var always = await CreateProductAsync("always", 250m, KycRequirementMode.Always, null, policyVersionId);
        using var client = _fixture.CreateClient(token);

        var belowOrder = await CheckoutAsync(client, [(below.Id, 1)]);
        var equalOrder = await CheckoutAsync(client, [(equal.Id, 1)]);
        var aboveOrder = await CheckoutAsync(client, [(above.Id, 1)]);
        var quantityOneOrder = await CheckoutAsync(client, [(quantity.Id, 1)]);
        var quantityTwoOrder = await CheckoutAsync(client, [(quantity.Id, 2)]);

        (await GetItemAsync(belowOrder)).Should().Match<OrderItem>(x => !x.RequiresVerification && x.KycEvaluatedAmount == 4_999m && x.KycThresholdAmount == null && x.KycPolicyVersionId == null);
        (await GetItemAsync(equalOrder)).Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 5_000m && x.KycThresholdAmount == 5_000m && x.KycPolicyVersionId == policyVersionId);
        (await GetItemAsync(aboveOrder)).Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 5_001m && x.KycThresholdAmount == 5_000m && x.KycPolicyVersionId == policyVersionId);
        (await GetItemAsync(quantityOneOrder)).Should().Match<OrderItem>(x => !x.RequiresVerification && x.KycEvaluatedAmount == 2_500m && x.KycThresholdAmount == null);
        (await GetItemAsync(quantityTwoOrder)).Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 5_000m && x.KycThresholdAmount == 5_000m);

        var coupon = new Coupon { Id = Guid.NewGuid(), Code = $"KYC{Guid.NewGuid():N}", Title = "KYC evidence", DiscountType = (byte)DiscountType.Percentage, DiscountValue = 10, MaxUsageCount = 10, MaxUsagePerUser = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        await using (var db = _fixture.CreateDbContext()) { db.Coupons.Add(coupon); await db.SaveChangesAsync(); }
        var couponOrder = await CheckoutAsync(client, [(equal.Id, 1)], coupon.Code);
        var couponItem = await GetItemAsync(couponOrder);
        couponItem.RequiresVerification.Should().BeFalse();
        couponItem.KycEvaluatedAmount.Should().Be(4_500m, "the order-total rule evaluates the final payable amount after a coupon");
        await using (var db = _fixture.CreateDbContext())
            (await db.Orders.SingleAsync(x => x.Id == couponOrder)).FinalAmount.Should().Be(4_500m);

        var mixedOrder = await CheckoutAsync(client, [(none.Id, 1), (below.Id, 1), (above.Id, 1), (always.Id, 1)]);
        await using var verify = _fixture.CreateDbContext();
        var mixed = await verify.OrderItems.Where(x => x.OrderId == mixedOrder).ToDictionaryAsync(x => x.ProductId);
        mixed.Values.Should().OnlyContain(x => x.RequiresVerification && x.KycRequirementMode == (byte)KycRequirementMode.AboveThreshold && x.KycPolicyVersionId == policyVersionId && x.KycThresholdAmount == 5_000m && x.KycEvaluatedAmount == 10_500m);
    }

    [Fact]
    public async Task Checkout_snapshots_storewide_threshold_history_without_mutating_the_first_order()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerifiedAsync(customer.Id);
        var v1 = await _fixture.ConfigureOrderTotalKycAsync(5_000m);
        var product = await CreateProductAsync("history", 5_000m, KycRequirementMode.None, null, null);
        using var client = _fixture.CreateClient(token);

        var firstOrder = await CheckoutAsync(client, [(product.Id, 1)]);
        await _fixture.ConfigureOrderTotalKycAsync(10_000m);
        var secondOrder = await CheckoutAsync(client, [(product.Id, 1)]);

        var first = await GetItemAsync(firstOrder);
        var second = await GetItemAsync(secondOrder);
        first.Should().Match<OrderItem>(x => x.KycPolicyVersionId == v1 && x.KycThresholdAmount == 5_000m && x.KycEvaluatedAmount == 5_000m && x.RequiresVerification);
        second.Should().Match<OrderItem>(x => x.KycPolicyVersionId == null && x.KycThresholdAmount == null && x.KycEvaluatedAmount == 5_000m && !x.RequiresVerification);
        await using var verify = _fixture.CreateDbContext();
        (await verify.KycPolicyVersions.Include(x => x.DocumentRequirements).SingleAsync(x => x.Id == v1)).Version.Should().Be(1);
    }

    [Fact]
    public async Task Admin_product_api_creates_products_without_a_kyc_configuration()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        using var client = _fixture.CreateClient(token);
        Guid categoryId;
        await using (var db = _fixture.CreateDbContext())
        {
            var category = new Category { Id = Guid.NewGuid(), Title = "FIX-09 validation", Slug = $"fix09-validation-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
            db.Categories.Add(category); await db.SaveChangesAsync();
            categoryId = category.Id;
        }

        var request = new CreateProductRequestDto
        {
            CategoryId = categoryId, Title = $"FIX-09 validation {Guid.NewGuid():N}", Slug = $"fix09-validation-{Guid.NewGuid():N}",
            ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 100m, CurrencyType = (byte)CurrencyType.Toman,
            IsActive = true
        };
        var response = await client.PostAsJsonAsync("/api/admin/products", request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var created = (await response.Content.ReadFromJsonAsync<ApiResult<AdminProductDto>>())!.Data!;
        created.Title.Should().Be(request.Title);
    }

    [Fact]
    public async Task Unverified_customer_can_checkout_when_the_current_cart_requires_kyc()
    {
        var version = await CreatePublishedPolicyVersionAsync("unverified");
        var none = await CreateProductAsync("unverified-none", 100m, KycRequirementMode.None, null, null);
        var below = await CreateProductAsync("unverified-below", 2_500m, KycRequirementMode.AboveThreshold, 5_000m, version);
        var triggered = await CreateProductAsync("unverified-triggered", 5_000m, KycRequirementMode.AboveThreshold, 5_000m, version);
        var always = await CreateProductAsync("unverified-always", 100m, KycRequirementMode.Always, null, version);

        async Task<HttpStatusCode> AttemptAsync(Guid productId, int quantity, string? coupon = null)
        {
            var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
            using var client = _fixture.CreateClient(token);
            (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = productId, Quantity = quantity })).StatusCode.Should().Be(HttpStatusCode.OK);
            client.DefaultRequestHeaders.Add("Idempotency-Key", $"unverified-{Guid.NewGuid():N}");
            return (await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = coupon })).StatusCode;
        }

        (await AttemptAsync(none.Id, 1)).Should().Be(HttpStatusCode.OK);
        (await AttemptAsync(below.Id, 1)).Should().Be(HttpStatusCode.OK);
        (await AttemptAsync(triggered.Id, 1)).Should().Be(HttpStatusCode.OK);
        (await AttemptAsync(always.Id, 1)).Should().Be(HttpStatusCode.OK);
        var coupon = new Coupon { Id = Guid.NewGuid(), Code = $"UNV{Guid.NewGuid():N}", Title = "Unverified KYC", DiscountType = (byte)DiscountType.Percentage, DiscountValue = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        await using (var db = _fixture.CreateDbContext()) { db.Coupons.Add(coupon); await db.SaveChangesAsync(); }
        (await AttemptAsync(triggered.Id, 1, coupon.Code)).Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Payment_retry_keeps_the_original_checkout_kyc_snapshot()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerifiedAsync(customer.Id);
        var v1 = await _fixture.ConfigureOrderTotalKycAsync(5_000m);
        var product = await CreateProductAsync("retry", 5_000m, KycRequirementMode.None, null, null);
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, [(product.Id, 1)]);
        var initial = await GetItemAsync(orderId);

        var start = await client.PostAsync($"/api/payments/start/{orderId}", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        var firstPayment = (await start.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
        firstPayment.Authority.Should().NotBeNullOrWhiteSpace();
        var cancelled = await client.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(firstPayment.Authority!)}&Status=NOK");
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());

        await _fixture.ConfigureOrderTotalKycAsync(10_000m);

        var retry = await client.PostAsync($"/api/payments/retry/{orderId}", null);
        retry.StatusCode.Should().Be(HttpStatusCode.OK, await retry.Content.ReadAsStringAsync());
        var retryPayment = (await retry.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
        var paid = await client.PostAsync($"/api/payments/mock/verify/{retryPayment.PaymentId}", null);
        paid.StatusCode.Should().Be(HttpStatusCode.OK, await paid.Content.ReadAsStringAsync());

        var persisted = await GetItemAsync(orderId);
        persisted.Should().Match<OrderItem>(x => x.Id == initial.Id && x.RequiresVerification == initial.RequiresVerification && x.KycRequirementMode == initial.KycRequirementMode && x.KycThresholdAmount == 5_000m && x.KycEvaluatedAmount == 5_000m && x.KycPolicyVersionId == v1);
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verify.Payments.CountAsync(x => x.OrderId == orderId)).Should().Be(2);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == initial.Id)).Status
            .Should().Be((byte)OrderItemKycStatus.Satisfied,
                "the retry payment must initialize from the original paid order-item snapshot, not the changed product");
    }

    private async Task<Guid> CheckoutAsync(HttpClient client, IReadOnlyList<(Guid ProductId, int Quantity)> items, string? couponCode = null)
    {
        foreach (var (productId, quantity) in items)
            (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = productId, Quantity = quantity })).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09p1-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!.OrderId;
    }

    private async Task<OrderItem> GetItemAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.OrderItems.SingleAsync(x => x.OrderId == orderId);
    }

    private async Task SetVerifiedAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        user.VerificationStatus = (byte)VerificationStatus.Verified;
        await db.SaveChangesAsync();
    }

    private async Task<(Guid PolicyId, Guid VersionId)> CreatePublishedPolicyAsync(string suffix)
    {
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"fix09-{suffix}-{Guid.NewGuid():N}", Name = "FIX-09 evidence", IsActive = true, CreatedAt = DateTime.UtcNow };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "FIX-09 V1", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        policy.Versions.Add(version);
        await using var db = _fixture.CreateDbContext();
        db.KycPolicies.Add(policy); await db.SaveChangesAsync();
        return (policy.Id, version.Id);
    }

    private async Task<Guid> CreatePublishedPolicyVersionAsync(string suffix, Guid? policyId = null, int version = 1)
    {
        if (!policyId.HasValue) return (await CreatePublishedPolicyAsync(suffix)).VersionId;
        var entity = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policyId.Value, Version = version, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = $"FIX-09 V{version}", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        await using var db = _fixture.CreateDbContext();
        db.KycPolicyVersions.Add(entity); await db.SaveChangesAsync();
        return entity.Id;
    }

    private async Task<Product> CreateProductAsync(string suffix, decimal price, KycRequirementMode mode, decimal? threshold, Guid? policyVersionId)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Title = "FIX-09 evidence", Slug = $"fix09-evidence-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = $"FIX-09 {suffix}", Slug = $"fix09-{suffix}-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual, BasePrice = price, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow, RequiresVerification = mode != KycRequirementMode.None, KycRequirementMode = (byte)mode, KycThresholdAmount = threshold, KycPolicyVersionId = policyVersionId };
        // Inventory is SKU-scoped: a purchasable non-Instant product always owns a canonical variant.
        product.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = price,
            StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 1000,
            IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
        });
        db.Categories.Add(category); db.Products.Add(product); await db.SaveChangesAsync();
        return product;
    }
}
