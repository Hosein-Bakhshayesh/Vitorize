using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Verifies that coupons affect the payable gateway amount only: the persisted
/// pre-payment KYC snapshot must retain the undiscounted item evaluation.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase1CouponGatewayRegressionIntegrationTests
{
    private const decimal UnitPrice = 5_000m;
    private const decimal KycThreshold = 4_000m;
    private const decimal CouponDiscount = 2_000m;
    private const decimal FinalPayable = UnitPrice - CouponDiscount;

    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase1CouponGatewayRegressionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Coupon_discounted_gateway_payment_preserves_kyc_snapshot_and_consumes_once()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerifiedAsync(customer.Id);
        var policyVersionId = await CreatePublishedPolicyVersionAsync();
        var (product, plaintextCode) = await CreateInstantKycProductAsync(policyVersionId);
        var coupon = await CreateCouponAsync();
        using var client = _fixture.CreateClient(token);

        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        add.StatusCode.Should().Be(HttpStatusCode.OK, await add.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09-coupon-checkout-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = coupon.Code });
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, await checkoutResponse.Content.ReadAsStringAsync());
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.Should().Match<CheckoutResultDto>(x => x.SubtotalAmount == UnitPrice &&
            x.DiscountAmount == CouponDiscount && x.FinalAmount == FinalPayable);

        OrderItem checkoutSnapshot;
        await using (var afterCheckout = _fixture.CreateDbContext())
        {
            (await afterCheckout.Orders.CountAsync(x => x.UserId == customer.Id)).Should().Be(1);
            (await afterCheckout.OrderItems.CountAsync(x => x.OrderId == checkout.OrderId)).Should().Be(1);
            (await afterCheckout.CartItems.CountAsync(x => x.Cart.UserId == customer.Id)).Should().Be(0);
            var order = await afterCheckout.Orders.SingleAsync(x => x.Id == checkout.OrderId);
            order.Should().Match<Order>(x => x.SubtotalAmount == UnitPrice && x.DiscountAmount == CouponDiscount &&
                x.FinalAmount == FinalPayable && x.CouponId == coupon.Id &&
                x.Status == (byte)OrderStatus.PendingPayment && x.PaymentStatus == (byte)PaymentStatus.Pending);
            checkoutSnapshot = await afterCheckout.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == checkout.OrderId);
            AssertKycSnapshot(checkoutSnapshot, policyVersionId);
            (await afterCheckout.CouponUsages.CountAsync(x => x.OrderId == checkout.OrderId)).Should().Be(0);
        }

        var firstAttempt = await StartAsync(client, checkout.OrderId);
        firstAttempt.Should().Match<PaymentStartResultDto>(x => x.Gateway == "Zarinpal" &&
            x.Amount == FinalPayable && x.Authority != null);
        await AssertGatewayAttemptAsync(firstAttempt.PaymentId, checkout.OrderId, firstAttempt.Authority!, PaymentStatus.Pending);

        var cancelled = await client.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(firstAttempt.Authority!)}&Status=NOK");
        cancelled.StatusCode.Should().Be(HttpStatusCode.OK, await cancelled.Content.ReadAsStringAsync());
        await using (var afterCancelled = _fixture.CreateDbContext())
        {
            (await afterCancelled.Payments.SingleAsync(x => x.Id == firstAttempt.PaymentId)).Status.Should().Be((byte)PaymentStatus.Cancelled);
            (await afterCancelled.CouponUsages.CountAsync(x => x.OrderId == checkout.OrderId)).Should().Be(0,
                "a failed/cancelled gateway attempt cannot consume the coupon");
        }

        var retryAttempt = await StartAsync(client, checkout.OrderId, retry: true);
        retryAttempt.Should().Match<PaymentStartResultDto>(x => x.PaymentId != firstAttempt.PaymentId &&
            x.Gateway == "Zarinpal" && x.Amount == FinalPayable && x.Authority != null);
        await AssertGatewayAttemptAsync(retryAttempt.PaymentId, checkout.OrderId, retryAttempt.Authority!, PaymentStatus.Pending);

        var callback = await client.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(retryAttempt.Authority!)}&Status=OK");
        callback.StatusCode.Should().Be(HttpStatusCode.OK, await callback.Content.ReadAsStringAsync());
        var verified = (await callback.Content.ReadFromJsonAsync<ApiResult<PaymentVerifyResultDto>>())!.Data!;
        verified.Should().Match<PaymentVerifyResultDto>(x => x.IsPaid && x.PaymentId == retryAttempt.PaymentId &&
            x.OrderId == checkout.OrderId && x.PaymentStatus == (byte)PaymentStatus.Paid &&
            x.OrderStatus == (byte)OrderStatus.Completed);

        await AssertFinalStateAsync(customer.Id, coupon.Id, checkout.OrderId, retryAttempt.PaymentId,
            checkoutSnapshot, policyVersionId, plaintextCode);

        var duplicate = await client.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(retryAttempt.Authority!)}&Status=OK");
        duplicate.StatusCode.Should().Be(HttpStatusCode.OK, await duplicate.Content.ReadAsStringAsync());
        await AssertFinalStateAsync(customer.Id, coupon.Id, checkout.OrderId, retryAttempt.PaymentId,
            checkoutSnapshot, policyVersionId, plaintextCode);

        var retryEligibility = await client.GetAsync($"/api/payments/retry-eligibility/{checkout.OrderId}");
        retryEligibility.StatusCode.Should().Be(HttpStatusCode.OK, await retryEligibility.Content.ReadAsStringAsync());
        (await retryEligibility.Content.ReadFromJsonAsync<ApiResult<PaymentRetryEligibilityDto>>())!.Data!
            .CanRetry.Should().BeFalse();
    }

    private async Task<PaymentStartResultDto> StartAsync(HttpClient client, Guid orderId, bool retry = false)
    {
        var response = await client.PostAsync($"/api/payments/{(retry ? "retry" : "start")}/{orderId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
    }

    private async Task AssertGatewayAttemptAsync(Guid paymentId, Guid orderId, string authority, PaymentStatus expectedStatus)
    {
        await using var db = _fixture.CreateDbContext();
        (await db.Payments.SingleAsync(x => x.Id == paymentId)).Should().Match<Payment>(x =>
            x.OrderId == orderId && x.Gateway == "Zarinpal" && x.Authority == authority &&
            x.Amount == FinalPayable && x.Status == (byte)expectedStatus);
    }

    private async Task AssertFinalStateAsync(Guid customerId, Guid couponId, Guid orderId, Guid paidPaymentId,
        OrderItem checkoutSnapshot, Guid policyVersionId, string plaintextCode)
    {
        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.SingleAsync(x => x.Id == orderId);
        order.Should().Match<Order>(x => x.Status == (byte)OrderStatus.Completed &&
            x.PaymentStatus == (byte)PaymentStatus.Paid && x.SubtotalAmount == UnitPrice &&
            x.DiscountAmount == CouponDiscount && x.FinalAmount == FinalPayable && x.CouponId == couponId);
        var snapshot = await verify.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == orderId);
        snapshot.Id.Should().Be(checkoutSnapshot.Id);
        AssertKycSnapshot(snapshot, policyVersionId);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == snapshot.Id)).Status
            .Should().Be((byte)OrderItemKycStatus.Satisfied);

        var attempts = await verify.Payments.Where(x => x.OrderId == orderId).ToListAsync();
        attempts.Should().HaveCount(2);
        attempts.Single(x => x.Id == paidPaymentId).Should().Match<Payment>(x =>
            x.Gateway == "Zarinpal" && x.Status == (byte)PaymentStatus.Paid && x.CallbackVerified &&
            x.Amount == FinalPayable && x.Authority != null);
        attempts.Count(x => x.Status == (byte)PaymentStatus.Paid).Should().Be(1);
        attempts.Should().NotContain(x => x.Gateway == "Wallet");
        (await verify.PaymentCallbacks.CountAsync(x => x.PaymentId == paidPaymentId)).Should().Be(1,
            "the duplicate successful callback has the same callback key and is recorded once");

        var usage = await verify.CouponUsages.Where(x => x.OrderId == orderId).ToListAsync();
        usage.Should().ContainSingle().Which.Should().Match<CouponUsage>(x =>
            x.UserId == customerId && x.CouponId == couponId && x.OrderId == orderId);
        (await verify.Coupons.SingleAsync(x => x.Id == couponId)).UsedCount.Should().Be(1);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == customerId)).Should().Be(0);

        var deliveries = await verify.OrderItemDeliveries.Where(x => x.OrderItem.OrderId == orderId).ToListAsync();
        var delivery = deliveries.Should().ContainSingle().Subject;
        var giftCode = await verify.GiftCodes.SingleAsync(x => x.ProductId == snapshot.ProductId);
        giftCode.Status.Should().Be((byte)GiftCodeStatus.Delivered);
        delivery.DeliveredContent.Should().NotBeNullOrWhiteSpace();
        using var scope = _fixture.Factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IEncryptionService>().Decrypt(delivery.DeliveredContent!).Should().Be(plaintextCode);
    }

    private static void AssertKycSnapshot(OrderItem item, Guid policyVersionId) =>
        item.Should().Match<OrderItem>(x => x.RequiresVerification &&
            x.KycRequirementMode == (byte)KycRequirementMode.AboveThreshold &&
            x.KycThresholdAmount == KycThreshold && x.KycEvaluatedAmount == UnitPrice &&
            x.KycPolicyVersionId == policyVersionId);

    private async Task SetVerifiedAsync(Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        (await db.Users.SingleAsync(x => x.Id == userId)).VerificationStatus = (byte)VerificationStatus.Verified;
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreatePublishedPolicyVersionAsync()
    {
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"fix09-coupon-{Guid.NewGuid():N}", Name = "FIX-09 coupon policy", IsActive = true, CreatedAt = DateTime.UtcNow };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Coupon KYC", CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow };
        policy.Versions.Add(version);
        await using var db = _fixture.CreateDbContext();
        db.KycPolicies.Add(policy);
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task<(Product Product, string PlaintextCode)> CreateInstantKycProductAsync(Guid policyVersionId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var plaintextCode = $"FIX09-COUPON-{Guid.NewGuid():N}";
        var category = new Category { Id = Guid.NewGuid(), Title = "FIX-09 coupon", Slug = $"fix09-coupon-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-09 coupon instant",
            Slug = $"fix09-coupon-instant-{Guid.NewGuid():N}", ProductType = (byte)ProductType.GiftCard,
            DeliveryType = (byte)DeliveryType.Instant, BasePrice = UnitPrice,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true,
            RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.AboveThreshold,
            KycThresholdAmount = KycThreshold, KycPolicyVersionId = policyVersionId, CreatedAt = DateTime.UtcNow
        };
        var code = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = encryption.Encrypt(plaintextCode),
            MaskedCode = "****" + plaintextCode[^4..], Status = (byte)GiftCodeStatus.Available, EncryptionVersion = 2,
            CodeHashFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextCode))), CreatedAt = DateTime.UtcNow
        };
        await using var db = _fixture.CreateDbContext();
        db.Categories.Add(category); db.Products.Add(product); db.GiftCodes.Add(code);
        await db.SaveChangesAsync();
        return (product, plaintextCode);
    }

    private async Task<Coupon> CreateCouponAsync()
    {
        var coupon = new Coupon
        {
            Id = Guid.NewGuid(), Code = $"FIX09C{Guid.NewGuid():N}", Title = "FIX-09 coupon",
            DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = CouponDiscount,
            MaxUsageCount = 5, MaxUsagePerUser = 1, MinOrderAmount = UnitPrice, IsActive = true,
            StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1), CreatedAt = DateTime.UtcNow
        };
        await using var db = _fixture.CreateDbContext();
        db.Coupons.Add(coupon);
        await db.SaveChangesAsync();
        return coupon;
    }
}
