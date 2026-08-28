using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Regression coverage for Phase-1 KYC snapshots in the existing order read model.
/// Orders are created and paid through the real checkout/payment endpoints; policy
/// and product records are deterministic test fixtures only.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase1OrderHistoryIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase1OrderHistoryIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Order_details_keep_v1_v2_retry_and_migrated_legacy_snapshots_compatible()
    {
        var (owner, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        await SetVerifiedAsync(owner.Id);
        var (policyId, v1, v2) = await CreateVersionsAsync();
        var orderTotalPolicyVersion = await _fixture.ConfigureOrderTotalKycAsync(5_000m);
        var product = await CreateProductAsync("history", v1, 5_000m);
        var retryProduct = await CreateProductAsync("retry", v1, 5_000m);
        var legacy = await CreateLegacyCompatibleProductAsync();
        using var ownerClient = _fixture.CreateClient(ownerToken);
        using var otherClient = _fixture.CreateClient(otherToken);
        using var adminClient = _fixture.CreateClient(adminToken);

        var v1Order = await CheckoutAndPayAsync(ownerClient, product.Id);
        await DeliverAsync(adminClient, v1Order, "FIX09 delivered V1");

        await SetProductPolicyAsync(product.Id, v2, 4_000m);
        await _fixture.ConfigureOrderTotalKycAsync(4_000m);
        var v2Order = await CheckoutAndPayAsync(ownerClient, product.Id);

        await _fixture.ConfigureOrderTotalKycAsync(5_000m);
        var retryOrder = await CheckoutAsync(ownerClient, retryProduct.Id);
        var cancelled = await StartAsync(ownerClient, retryOrder);
        (await ownerClient.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(cancelled.Authority!)}&Status=NOK"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        await SetProductPolicyAsync(retryProduct.Id, v2, 10_000m);
        await _fixture.ConfigureOrderTotalKycAsync(10_000m);
        await PayAsync(ownerClient, retryOrder, retry: true);

        await _fixture.ConfigureOrderTotalKycAsync(1m);
        var legacyOrder = await CheckoutAndPayAsync(ownerClient, legacy.Id);

        foreach (var orderId in new[] { v1Order, v2Order, retryOrder, legacyOrder })
        {
            var ownerResponse = await ownerClient.GetAsync($"/api/orders/{orderId}");
            ownerResponse.StatusCode.Should().Be(HttpStatusCode.OK, await ownerResponse.Content.ReadAsStringAsync());
            var ownerDetail = (await ownerResponse.Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;
            ownerDetail.Items.Should().ContainSingle();

            var adminResponse = await adminClient.GetAsync($"/api/admin/orders/{orderId}");
            adminResponse.StatusCode.Should().Be(HttpStatusCode.OK, await adminResponse.Content.ReadAsStringAsync());
            (await adminResponse.Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!.Items.Should().ContainSingle();
        }

        (await otherClient.GetAsync($"/api/orders/{v1Order}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        await using var verify = _fixture.CreateDbContext();
        var items = await verify.OrderItems.Where(x => new[] { v1Order, v2Order, retryOrder, legacyOrder }.Contains(x.OrderId))
            .ToDictionaryAsync(x => x.OrderId);
        items[v1Order].Should().Match<OrderItem>(x => x.KycPolicyVersionId == orderTotalPolicyVersion && x.KycThresholdAmount == 5_000m && x.KycEvaluatedAmount == 5_000m && x.RequiresVerification);
        items[v2Order].Should().Match<OrderItem>(x => x.KycPolicyVersionId == orderTotalPolicyVersion && x.KycThresholdAmount == 4_000m && x.KycEvaluatedAmount == 5_000m && x.RequiresVerification);
        items[retryOrder].Should().Match<OrderItem>(x => x.KycPolicyVersionId == orderTotalPolicyVersion && x.KycThresholdAmount == 5_000m && x.KycEvaluatedAmount == 5_000m && x.RequiresVerification);
        items[legacyOrder].Should().Match<OrderItem>(x => x.KycRequirementMode == (byte)KycRequirementMode.AboveThreshold && x.KycThresholdAmount == 1m && x.KycEvaluatedAmount == 5_000m && x.KycPolicyVersionId == orderTotalPolicyVersion);
        // Product records intentionally no longer carry KYC policy state; retries use the
        // snapshot captured from the store-wide rule at the original checkout.
        (await verify.Products.SingleAsync(x => x.Id == retryProduct.Id)).Should().Match<Product>(x =>
            x.KycPolicyVersionId == null && x.KycThresholdAmount == null && !x.RequiresVerification);
        (await verify.Payments.CountAsync(x => x.OrderId == retryOrder)).Should().Be(2);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItem.OrderId == v1Order)).Should().Be(1);
        (await verify.KycPolicyVersions.SingleAsync(x => x.Id == v1)).KycPolicyId.Should().Be(policyId);
        (await verify.KycPolicyVersions.SingleAsync(x => x.Id == v2)).KycPolicyId.Should().Be(policyId);
    }

    private async Task<Guid> CheckoutAndPayAsync(HttpClient client, Guid productId)
    {
        var orderId = await CheckoutAsync(client, productId);
        await PayAsync(client, orderId, retry: false);
        return orderId;
    }

    private async Task<Guid> CheckoutAsync(HttpClient client, Guid productId)
    {
        (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = productId, Quantity = 1 }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09-history-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!.OrderId;
    }

    private static async Task<PaymentStartResultDto> StartAsync(HttpClient client, Guid orderId)
    {
        var response = await client.PostAsync($"/api/payments/start/{orderId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
    }

    private static async Task PayAsync(HttpClient client, Guid orderId, bool retry)
    {
        var start = retry
            ? await client.PostAsync($"/api/payments/retry/{orderId}", null)
            : await client.PostAsync($"/api/payments/start/{orderId}", null);
        start.StatusCode.Should().Be(HttpStatusCode.OK, await start.Content.ReadAsStringAsync());
        var payment = (await start.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
        (await client.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private static async Task DeliverAsync(HttpClient adminClient, Guid orderId, string content)
    {
        var detail = (await (await adminClient.GetAsync($"/api/admin/orders/{orderId}")).Content.ReadFromJsonAsync<ApiResult<OrderDto>>())!.Data!;
        (await adminClient.PostAsJsonAsync($"/api/admin/orders/{orderId}/deliver-manual", new { orderItemId = detail.Items.Single().Id, content, isVisibleToCustomer = true }))
            .StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task SetVerifiedAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        (await db.Users.SingleAsync(x => x.Id == userId)).VerificationStatus = (byte)VerificationStatus.Verified;
        await db.SaveChangesAsync();
    }

    private async Task SetProductPolicyAsync(Guid productId, Guid versionId, decimal threshold)
    {
        await using var db = _fixture.CreateDbContext();
        var product = await db.Products.SingleAsync(x => x.Id == productId);
        product.KycPolicyVersionId = versionId;
        product.KycThresholdAmount = threshold;
        await db.SaveChangesAsync();
    }

    private async Task<(Guid PolicyId, Guid V1, Guid V2)> CreateVersionsAsync()
    {
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"fix09-history-{Guid.NewGuid():N}", Name = "FIX-09 order history", IsActive = true, CreatedAt = DateTime.UtcNow };
        var v1 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V1", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        var v2 = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 2, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "V2", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        policy.Versions.Add(v1); policy.Versions.Add(v2);
        await using var db = _fixture.CreateDbContext(); db.KycPolicies.Add(policy); await db.SaveChangesAsync();
        return (policy.Id, v1.Id, v2.Id);
    }

    private async Task<Product> CreateProductAsync(string name, Guid versionId, decimal threshold)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category { Id = Guid.NewGuid(), Title = "FIX-09 history", Slug = $"fix09-history-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = $"FIX-09 {name}", Slug = $"fix09-{name}-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 5_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.AboveThreshold, KycThresholdAmount = threshold, KycPolicyVersionId = versionId, CreatedAt = DateTime.UtcNow };
        AddCanonicalVariant(product);
        db.Categories.Add(category); db.Products.Add(product); await db.SaveChangesAsync(); return product;
    }

    private async Task<Product> CreateLegacyCompatibleProductAsync()
    {
        await using var db = _fixture.CreateDbContext();
        var legacyVersion = await db.KycPolicyVersions.SingleAsync(x => x.KycPolicy.Code == "legacy-profile-verification" && x.Version == 1);
        var category = new Category { Id = Guid.NewGuid(), Title = "FIX-09 legacy", Slug = $"fix09-legacy-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-09 migrated legacy", Slug = $"fix09-legacy-{Guid.NewGuid():N}", ProductType = (byte)ProductType.Other, DeliveryType = (byte)DeliveryType.Manual, BasePrice = 5_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.Always, KycPolicyVersionId = legacyVersion.Id, CreatedAt = DateTime.UtcNow };
        AddCanonicalVariant(product);
        db.Categories.Add(category); db.Products.Add(product); await db.SaveChangesAsync(); return product;
    }

    /// <summary>
    /// Inventory is SKU-scoped: a purchasable non-Instant product always owns a canonical variant.
    /// Stock sits far above anything these tests order so the subject stays the KYC snapshot history.
    /// </summary>
    private static void AddCanonicalVariant(Product product) => product.ProductVariants.Add(new ProductVariant
    {
        Id = Guid.NewGuid(), ProductId = product.Id, Title = "پیش‌فرض", Price = product.BasePrice,
        StockMode = (byte)ProductVariantStockMode.Manual, StockQuantity = 1000,
        IsDefault = true, IsActive = true, SortOrder = 0, CreatedAt = DateTime.UtcNow
    });
}
