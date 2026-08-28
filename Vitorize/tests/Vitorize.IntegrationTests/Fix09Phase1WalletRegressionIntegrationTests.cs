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
/// Guards the wallet-only path through the real checkout, payment and instant
/// fulfillment pipeline. The initial gateway row is checkout history only;
/// the Wallet row is the sole authoritative paid attempt.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase1WalletRegressionIntegrationTests
{
    private const decimal InitialWalletBalance = 20_000m;
    private const decimal ProductPrice = 5_000m;

    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase1WalletRegressionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Wallet_only_payment_debits_once_preserves_kyc_snapshot_and_delivers_once_on_replay()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerifiedWalletBalanceAsync(customer.Id, InitialWalletBalance);
        var policyVersionId = await _fixture.ConfigureOrderTotalKycAsync(ProductPrice);
        var (product, plaintextCode) = await CreateKycInstantProductAsync(policyVersionId);
        using var client = _fixture.CreateClient(token);

        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto
        {
            ProductId = product.Id,
            Quantity = 1
        });
        add.StatusCode.Should().Be(HttpStatusCode.OK, await add.Content.ReadAsStringAsync());

        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09-wallet-checkout-{Guid.NewGuid():N}");
        var checkoutResponse = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto());
        checkoutResponse.StatusCode.Should().Be(HttpStatusCode.OK, await checkoutResponse.Content.ReadAsStringAsync());
        var checkout = (await checkoutResponse.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!;
        checkout.FinalAmount.Should().Be(ProductPrice);

        Guid initialPaymentId;
        OrderItem checkoutSnapshot;
        await using (var beforePayment = _fixture.CreateDbContext())
        {
            (await beforePayment.Orders.CountAsync(x => x.UserId == customer.Id)).Should().Be(1);
            (await beforePayment.CartItems.CountAsync(x => x.Cart.UserId == customer.Id)).Should().Be(0,
                "the real checkout clears the authenticated cart after creating its order");
            var order = await beforePayment.Orders.SingleAsync(x => x.Id == checkout.OrderId);
            order.Should().Match<Order>(x => x.CouponId == null && x.DiscountAmount == 0m &&
                                           x.FinalAmount == ProductPrice &&
                                           x.PaymentStatus == (byte)PaymentStatus.Pending);
            initialPaymentId = await beforePayment.Payments.Where(x => x.OrderId == checkout.OrderId)
                .Select(x => x.Id).SingleAsync();
            checkoutSnapshot = await beforePayment.OrderItems.AsNoTracking()
                .SingleAsync(x => x.OrderId == checkout.OrderId);
            checkoutSnapshot.Should().Match<OrderItem>(x => x.RequiresVerification &&
                x.KycRequirementMode == (byte)KycRequirementMode.AboveThreshold &&
                x.KycThresholdAmount == ProductPrice && x.KycEvaluatedAmount == ProductPrice &&
                x.KycPolicyVersionId == policyVersionId);
        }

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09-wallet-pay-{Guid.NewGuid():N}");
        var walletPaymentResponse = await client.PostAsync($"/api/payments/wallet/pay/{checkout.OrderId}", null);
        walletPaymentResponse.StatusCode.Should().Be(HttpStatusCode.OK, await walletPaymentResponse.Content.ReadAsStringAsync());
        var walletPayment = (await walletPaymentResponse.Content.ReadFromJsonAsync<ApiResult<PaymentVerifyResultDto>>())!.Data!;
        walletPayment.Should().Match<PaymentVerifyResultDto>(x => x.OrderId == checkout.OrderId && x.IsPaid &&
            x.PaymentStatus == (byte)PaymentStatus.Paid && x.OrderStatus == (byte)OrderStatus.Completed);

        await AssertFinalStateAsync(customer.Id, checkout.OrderId, initialPaymentId, checkoutSnapshot,
            policyVersionId, plaintextCode);

        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"fix09-wallet-replay-{Guid.NewGuid():N}");
        var replay = await client.PostAsync($"/api/payments/wallet/pay/{checkout.OrderId}", null);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest, await replay.Content.ReadAsStringAsync());

        await AssertFinalStateAsync(customer.Id, checkout.OrderId, initialPaymentId, checkoutSnapshot,
            policyVersionId, plaintextCode);

        var eligibility = await client.GetAsync($"/api/payments/retry-eligibility/{checkout.OrderId}");
        eligibility.StatusCode.Should().Be(HttpStatusCode.OK, await eligibility.Content.ReadAsStringAsync());
        var retry = (await eligibility.Content.ReadFromJsonAsync<ApiResult<PaymentRetryEligibilityDto>>())!.Data!;
        retry.Should().Match<PaymentRetryEligibilityDto>(x => x.OrderId == checkout.OrderId && !x.CanRetry);
    }

    private async Task AssertFinalStateAsync(Guid customerId, Guid orderId, Guid initialPaymentId,
        OrderItem checkoutSnapshot, Guid policyVersionId, string plaintextCode)
    {
        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.SingleAsync(x => x.Id == orderId);
        order.Should().Match<Order>(x => x.Status == (byte)OrderStatus.Completed &&
            x.PaymentStatus == (byte)PaymentStatus.Paid && x.FinalAmount == ProductPrice &&
            x.CouponId == null && x.DiscountAmount == 0m);

        var snapshot = await verify.OrderItems.AsNoTracking().SingleAsync(x => x.OrderId == orderId);
        snapshot.Should().Match<OrderItem>(x => x.Id == checkoutSnapshot.Id &&
            x.RequiresVerification == checkoutSnapshot.RequiresVerification &&
            x.KycRequirementMode == checkoutSnapshot.KycRequirementMode &&
            x.KycThresholdAmount == ProductPrice && x.KycEvaluatedAmount == ProductPrice &&
            x.KycPolicyVersionId == policyVersionId);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == snapshot.Id)).Status
            .Should().Be((byte)OrderItemKycStatus.Satisfied);

        var payments = await verify.Payments.Where(x => x.OrderId == orderId).ToListAsync();
        payments.Should().HaveCount(2, "checkout keeps one pending historical gateway attempt and wallet adds one paid attempt");
        payments.Single(x => x.Id == initialPaymentId).Should().Match<Payment>(x =>
            x.Status == (byte)PaymentStatus.Pending && x.Gateway == "Zarinpal" && x.Authority == null);
        payments.Where(x => x.Status == (byte)PaymentStatus.Paid).Should().ContainSingle()
            .Which.Should().Match<Payment>(x => x.Gateway == "Wallet" && x.CallbackVerified &&
                x.Amount == ProductPrice && x.Authority != null && x.Authority.StartsWith("WALLET-"));
        payments.Where(x => x.Gateway != "Wallet").Should().OnlyContain(x => x.Authority == null,
            "wallet-only payment must never create external gateway authority");

        var wallet = await verify.Wallets.SingleAsync(x => x.UserId == customerId);
        wallet.Balance.Should().Be(InitialWalletBalance - ProductPrice);
        var debits = await verify.WalletTransactions.Where(x => x.UserId == customerId &&
            x.Type == (byte)WalletTransactionType.Debit).ToListAsync();
        debits.Should().ContainSingle().Which.Should().Match<WalletTransaction>(x =>
            x.Amount == ProductPrice && x.ReferenceType == (byte)WalletReferenceType.OrderPayment &&
            x.ReferenceId == orderId && x.BalanceAfter == InitialWalletBalance - ProductPrice);

        var deliveries = await verify.OrderItemDeliveries.Where(x => x.OrderItem.OrderId == orderId).ToListAsync();
        var delivery = deliveries.Should().ContainSingle().Subject;
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderId == orderId &&
            x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(1);
        var delivered = await verify.GiftCodes.SingleAsync(x => x.ProductId == snapshot.ProductId);
        delivered.Status.Should().Be((byte)GiftCodeStatus.Delivered);
        using var scope = _fixture.Factory.Services.CreateScope();
        delivery.DeliveredContent.Should().NotBeNullOrWhiteSpace();
        scope.ServiceProvider.GetRequiredService<IEncryptionService>().Decrypt(delivery.DeliveredContent!)
            .Should().Be(plaintextCode);
    }

    private async Task SetVerifiedWalletBalanceAsync(Guid userId, decimal balance)
    {
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        user.VerificationStatus = (byte)VerificationStatus.Verified;
        db.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = balance, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> CreatePublishedPolicyVersionAsync()
    {
        var policy = new KycPolicy
        {
            Id = Guid.NewGuid(), Code = $"fix09-wallet-{Guid.NewGuid():N}", Name = "FIX-09 wallet policy",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var version = new KycPolicyVersion
        {
            Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1,
            Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Wallet KYC",
            CreatedAt = DateTime.UtcNow, PublishedAt = DateTime.UtcNow
        };
        policy.Versions.Add(version);
        await using var db = _fixture.CreateDbContext();
        db.KycPolicies.Add(policy);
        await db.SaveChangesAsync();
        return version.Id;
    }

    private async Task<(Product Product, string PlaintextCode)> CreateKycInstantProductAsync(Guid policyVersionId)
    {
        using var scope = _fixture.Factory.Services.CreateScope();
        var encryption = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
        var plaintextCode = $"FIX09-WALLET-{Guid.NewGuid():N}";
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "FIX-09 wallet", Slug = $"fix09-wallet-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "FIX-09 wallet instant",
            Slug = $"fix09-wallet-instant-{Guid.NewGuid():N}", ProductType = (byte)ProductType.GiftCard,
            DeliveryType = (byte)DeliveryType.Instant, BasePrice = ProductPrice,
            CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true,
            RequiresVerification = true, KycRequirementMode = (byte)KycRequirementMode.AboveThreshold,
            KycThresholdAmount = ProductPrice, KycPolicyVersionId = policyVersionId, CreatedAt = DateTime.UtcNow
        };
        var code = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = encryption.Encrypt(plaintextCode),
            MaskedCode = "****" + plaintextCode[^4..], Status = (byte)GiftCodeStatus.Available,
            EncryptionVersion = 2,
            CodeHashFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintextCode))),
            CreatedAt = DateTime.UtcNow
        };
        await using var db = _fixture.CreateDbContext();
        db.Categories.Add(category);
        db.Products.Add(product);
        db.GiftCodes.Add(code);
        await db.SaveChangesAsync();
        return (product, plaintextCode);
    }
}
