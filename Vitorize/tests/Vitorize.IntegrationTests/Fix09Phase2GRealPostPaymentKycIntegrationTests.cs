using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Api.Controllers;
using Vitorize.Application.Cart;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.DTOs.Payments;
using Vitorize.Application.DTOs.Verification;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Activation-level evidence for the real pay-first flow.  These tests deliberately
/// use checkout and payment endpoints; the earlier Phase 2B-2F suites cover the
/// same services in isolation and are rerun with this class by the QA command.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix09Phase2GRealPostPaymentKycIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public Fix09Phase2GRealPostPaymentKycIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Unverified_customer_can_pay_and_enters_awaiting_submission_from_the_checkout_snapshot()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 5_000m, KycRequirementMode.AboveThreshold, 10_000m, giftCodes: 2);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id, 2);

        await using var verify = _fixture.CreateDbContext();
        var order = await verify.Orders.SingleAsync(x => x.Id == orderId);
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        var state = await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id);
        order.PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        order.Status.Should().NotBe((byte)OrderStatus.Completed);
        item.Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 10_000m && x.KycThresholdAmount == 10_000m && x.KycPolicyVersionId == setup.PolicyVersion.Id);
        state.Status.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(2);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
        (await verify.Notifications.CountAsync(x => x.UserId == customer.Id && x.Type == (byte)NotificationType.GiftCodeDelivered)).Should().Be(0);
    }

    [Fact]
    public async Task Verified_customer_is_satisfied_and_instant_fulfillment_is_not_held()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        await SetVerificationStatusAsync(customer.Id, VerificationStatus.Verified);
        var setup = await CreateProductAsync(DeliveryType.Instant, 10_000m, KycRequirementMode.Always, null, giftCodes: 1);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id);

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        item.RequiresVerification.Should().BeTrue();
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(1);
        (await verify.Orders.SingleAsync(x => x.Id == orderId)).Status.Should().Be((byte)OrderStatus.Completed);
    }

    [Theory]
    [InlineData(KycRequirementMode.None, 0, 9_999, 1)]
    [InlineData(KycRequirementMode.AboveThreshold, 10_000, 9_999, 1)]
    public async Task No_kyc_and_below_threshold_items_pay_and_fulfill_normally(KycRequirementMode mode, decimal threshold, decimal unitPrice, int quantity)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, unitPrice, mode, threshold == 0 ? null : threshold, giftCodes: quantity);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id, quantity);

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        item.RequiresVerification.Should().BeFalse();
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id)).Status.Should().Be((byte)OrderItemKycStatus.NotRequired);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(1);
    }

    [Theory]
    [InlineData(10_000, 1)]
    [InlineData(5_000, 2)]
    public async Task Inclusive_and_quantity_thresholds_create_a_held_post_payment_item(decimal unitPrice, int quantity)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, unitPrice, KycRequirementMode.AboveThreshold, 10_000m, giftCodes: quantity);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id, quantity);

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        item.Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 10_000m && x.KycThresholdAmount == 10_000m);
        (await verify.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Coupon_gateway_and_duplicate_callback_keep_the_undiscounted_snapshot_and_one_held_allocation()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 10_000m, KycRequirementMode.AboveThreshold, 8_000m, giftCodes: 1);
        var coupon = await CreateCouponAsync(5_000m, 10_000m);
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, setup.Product.Id, 1, coupon.Code);
        var payment = await StartAsync(client, orderId);
        (await client.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        item.Should().Match<OrderItem>(x => x.RequiresVerification && x.KycEvaluatedAmount == 10_000m && x.KycThresholdAmount == 8_000m);
        (await verify.CouponUsages.Where(x => x.OrderId == orderId).ToListAsync()).Should().ContainSingle(x => x.UserId == customer.Id && x.CouponId == coupon.Id);
        (await verify.Payments.CountAsync(x => x.OrderId == orderId && x.Status == (byte)PaymentStatus.Paid)).Should().Be(1);
        (await verify.OrderItemKycStates.Where(x => x.OrderItemId == item.Id).ToListAsync()).Should().ContainSingle(x => x.Status == (byte)OrderItemKycStatus.AwaitingSubmission);
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(1);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
    }

    [Fact]
    public async Task Wallet_only_payment_holds_then_releases_exactly_once_after_real_verification_approval()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var setup = await CreateProductAsync(DeliveryType.Instant, 5_000m, KycRequirementMode.Always, null, giftCodes: 1, requiresDocument: true);
        await AddWalletAsync(customer.Id, 10_000m);
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, setup.Product.Id);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"p2g-wallet-{Guid.NewGuid():N}");
        (await client.PostAsync($"/api/payments/wallet/pay/{orderId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var held = _fixture.CreateDbContext())
        {
            var item = await held.OrderItems.SingleAsync(x => x.OrderId == orderId);
            (await held.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id)).Status.Should().Be((byte)OrderItemKycStatus.AwaitingSubmission);
            (await held.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
            (await held.WalletTransactions.CountAsync(x => x.UserId == customer.Id && x.Type == (byte)WalletTransactionType.Debit)).Should().Be(1);
        }

        await SubmitAndApproveAsync(customer.Id, admin.Id, setup.DocumentType!);
        await using var released = _fixture.CreateDbContext();
        var releasedItem = await released.OrderItems.SingleAsync(x => x.OrderId == orderId);
        (await released.OrderItemKycStates.SingleAsync(x => x.OrderItemId == releasedItem.Id)).Status.Should().Be((byte)OrderItemKycStatus.Satisfied);
        (await released.OrderItemDeliveries.CountAsync(x => x.OrderItemId == releasedItem.Id)).Should().Be(1);
        (await released.WalletTransactions.CountAsync(x => x.UserId == customer.Id && x.Type == (byte)WalletTransactionType.Debit)).Should().Be(1);
    }

    [Fact]
    public async Task Insufficient_wallet_does_not_create_a_partial_debit_or_paid_lifecycle()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 10_000m, KycRequirementMode.Always, null, giftCodes: 1);
        await AddWalletAsync(customer.Id, 1m);
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, setup.Product.Id);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"p2g-wallet-insufficient-{Guid.NewGuid():N}");
        (await client.PostAsync($"/api/payments/wallet/pay/{orderId}", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
        (await verify.WalletTransactions.CountAsync(x => x.UserId == customer.Id && x.Type == (byte)WalletTransactionType.Debit)).Should().Be(0);
        (await verify.OrderItemKycStates.CountAsync(x => x.OrderItem.OrderId == orderId)).Should().Be(0);
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderId == orderId && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(0);
    }

    [Fact]
    public async Task Cancelled_gateway_attempt_is_retryable_and_successful_retry_initializes_one_held_lifecycle()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 10_000m, KycRequirementMode.Always, null, giftCodes: 1);
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, setup.Product.Id);
        var first = await StartAsync(client, orderId);
        (await client.GetAsync($"/api/payments/zarinpal/callback?Authority={Uri.EscapeDataString(first.Authority!)}&Status=NOK")).StatusCode.Should().Be(HttpStatusCode.OK);
        await using (var cancelled = _fixture.CreateDbContext())
        {
            (await cancelled.OrderItemKycStates.CountAsync(x => x.OrderItem.OrderId == orderId)).Should().Be(0);
            (await cancelled.GiftCodeReservations.CountAsync(x => x.OrderId == orderId && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(0);
        }
        var retry = await StartAsync(client, orderId, retry: true);
        (await client.PostAsync($"/api/payments/mock/verify/{retry.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var paid = _fixture.CreateDbContext();
        var item = await paid.OrderItems.SingleAsync(x => x.OrderId == orderId);
        (await paid.OrderItemKycStates.Where(x => x.OrderItemId == item.Id).ToListAsync()).Should().ContainSingle(x => x.Status == (byte)OrderItemKycStatus.AwaitingSubmission);
        (await paid.GiftCodeReservations.CountAsync(x => x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(1);
        (await paid.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
        (await paid.Orders.CountAsync(x => x.Id == orderId)).Should().Be(1);
    }

    [Fact]
    public async Task Mixed_order_fulfills_only_no_kyc_item_until_the_shared_policy_is_satisfied()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var kyc = await CreateKycSetupAsync(requiresDocument: true);
        var noKyc = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.None, null, giftCodes: 1);
        var instant = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.Always, null, giftCodes: 1, setup: kyc);
        var manual = await CreateProductAsync(DeliveryType.Manual, 1_000m, KycRequirementMode.Always, null, setup: kyc);
        var support = await CreateProductAsync(DeliveryType.SupportRequired, 1_000m, KycRequirementMode.Always, null, setup: kyc);
        using var client = _fixture.CreateClient(token);
        foreach (var product in new[] { noKyc.Product, instant.Product, manual.Product, support.Product })
            (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        var orderId = await CheckoutAsync(client, noKyc.Product.Id, checkoutOnly: true);
        var payment = await StartAsync(client, orderId);
        (await client.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);

        await using (var held = _fixture.CreateDbContext())
        {
            var items = await held.OrderItems.Where(x => x.OrderId == orderId).ToListAsync();
            var normal = items.Single(x => x.ProductId == noKyc.Product.Id);
            var heldInstant = items.Single(x => x.ProductId == instant.Product.Id);
            (await held.OrderItemDeliveries.CountAsync(x => x.OrderItemId == normal.Id)).Should().Be(1);
            (await held.OrderItemDeliveries.CountAsync(x => x.OrderItemId == heldInstant.Id)).Should().Be(0);
            (await held.OrderItemKycStates.Where(x => x.OrderItemId != normal.Id && x.OrderItem.OrderId == orderId).ToListAsync()).Should().OnlyContain(x => x.Status == (byte)OrderItemKycStatus.AwaitingSubmission);
            (await held.Tickets.CountAsync(x => x.OrderId == orderId && x.IsFulfillmentTicket)).Should().Be(0);
            (await held.Orders.SingleAsync(x => x.Id == orderId)).Status.Should().NotBe((byte)OrderStatus.Completed);
        }
        await SubmitAndApproveAsync(customer.Id, admin.Id, kyc.DocumentType!);
        await using var released = _fixture.CreateDbContext();
        var after = await released.OrderItems.Where(x => x.OrderId == orderId).ToListAsync();
        var releasedInstantItemId = after.Single(x => x.ProductId == instant.Product.Id).Id;
        var releasedManualItemId = after.Single(x => x.ProductId == manual.Product.Id).Id;
        (await released.OrderItemDeliveries.CountAsync(x => x.OrderItemId == releasedInstantItemId)).Should().Be(1);
        (await released.OrderItemDeliveries.CountAsync(x => x.OrderItemId == releasedManualItemId)).Should().Be(0);
        (await released.Tickets.CountAsync(x => x.OrderId == orderId && x.IsFulfillmentTicket)).Should().Be(1);
    }

    [Fact]
    public async Task Real_paid_item_can_be_rejected_then_resubmitted_without_releasing_its_allocation()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var setup = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.Always, null, giftCodes: 1, requiresDocument: true);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id);
        var profileId = await SubmitForReviewAsync(customer.Id, setup.DocumentType!);
        using (var scope = _fixture.Factory.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IVerificationService>().ReviewAsync(profileId, admin.Id, new ReviewVerificationRequestDto { Approve = false });
        await using (var rejected = _fixture.CreateDbContext())
        {
            var item = await rejected.OrderItems.SingleAsync(x => x.OrderId == orderId);
            (await rejected.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id)).Status.Should().Be((byte)OrderItemKycStatus.Rejected);
            (await rejected.GiftCodeReservations.CountAsync(x => x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(1);
            (await rejected.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
            (await rejected.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        }
        await SubmitForReviewAsync(customer.Id, setup.DocumentType!);
        await using var resubmitted = _fixture.CreateDbContext();
        (await resubmitted.OrderItemKycStates.Where(x => x.OrderItem.OrderId == orderId).ToListAsync()).Should().ContainSingle(x => x.Status == (byte)OrderItemKycStatus.AwaitingReview);
    }

    [Fact]
    public async Task Final_rejected_paid_item_retains_its_allocation_without_delivery_or_refund()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.Always, null, giftCodes: 1);
        var orderId = await CheckoutAndPayAsync(token, setup.Product.Id);
        await using (var transition = _fixture.CreateDbContext())
        {
            var state = await transition.OrderItemKycStates.SingleAsync(x => x.OrderItem.OrderId == orderId);
            state.Status = (byte)OrderItemKycStatus.FinalRejected;
            state.UpdatedAt = DateTime.UtcNow;
            await transition.SaveChangesAsync();
        }
        await using var verify = _fixture.CreateDbContext();
        var item = await verify.OrderItems.SingleAsync(x => x.OrderId == orderId);
        (await verify.Orders.SingleAsync(x => x.Id == orderId)).PaymentStatus.Should().Be((byte)PaymentStatus.Paid);
        (await verify.GiftCodeReservations.CountAsync(x => x.OrderItemId == item.Id && x.Status == (byte)GiftCodeReservationStatus.Sold)).Should().Be(1);
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == item.Id)).Should().Be(0);
        (await verify.Payments.CountAsync(x => x.OrderId == orderId && x.Status == (byte)PaymentStatus.Refunded)).Should().Be(0);
    }

    [Fact]
    public async Task Paid_manual_item_is_blocked_for_every_held_state_and_allowed_only_when_satisfied()
    {
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var setup = await CreateProductAsync(DeliveryType.Manual, 1_000m, KycRequirementMode.Always, null);
        var orderId = await CheckoutAndPayAsync(customerToken, setup.Product.Id);
        await using var db = _fixture.CreateDbContext();
        var item = await db.OrderItems.SingleAsync(x => x.OrderId == orderId);
        using var admin = _fixture.CreateClient(adminToken);
        foreach (var status in new[] { OrderItemKycStatus.AwaitingSubmission, OrderItemKycStatus.AwaitingReview, OrderItemKycStatus.Rejected, OrderItemKycStatus.FinalRejected })
        {
            var state = await db.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id);
            state.Status = (byte)status; state.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
            (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = item.Id, Content = status.ToString() })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        var satisfied = await db.OrderItemKycStates.SingleAsync(x => x.OrderItemId == item.Id);
        satisfied.Status = (byte)OrderItemKycStatus.Satisfied; satisfied.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        (await admin.PostAsJsonAsync($"/api/admin/orders/{orderId}/deliver-manual", new ManualDeliveryRequestDto { OrderItemId = item.Id, Content = "released" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Other_customer_cannot_read_a_real_paid_held_order_or_its_secret()
    {
        var (_, ownerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, otherToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.Always, null, giftCodes: 1);
        var orderId = await CheckoutAndPayAsync(ownerToken, setup.Product.Id);
        using var other = _fixture.CreateClient(otherToken);
        (await other.GetAsync($"/api/orders/{orderId}")).StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.NotFound);
        (await other.GetAsync("/api/orders/deliveries")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await (await other.GetAsync("/api/orders/deliveries")).Content.ReadAsStringAsync()).Should().NotContain(orderId.ToString());
    }

    [Fact]
    public async Task Guest_cart_merge_then_checkout_uses_the_same_informational_post_payment_path()
    {
        var setup = await CreateProductAsync(DeliveryType.Instant, 1_000m, KycRequirementMode.Always, null, giftCodes: 1);
        var guestToken = GuestCartToken.Create();
        using var guest = _fixture.CreateClient();
        guest.DefaultRequestHeaders.Add("X-Vitorize-Guest-Cart", guestToken);
        (await guest.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = setup.Product.Id, Quantity = 1 })).StatusCode.Should().Be(HttpStatusCode.OK);
        (await guest.GetAsync("/api/cart")).StatusCode.Should().Be(HttpStatusCode.OK);
        var (_, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var customer = _fixture.CreateClient(customerToken);
        (await customer.PostAsJsonAsync("/api/cart/merge-guest", new CartController.MergeGuestCartRequest(guestToken))).StatusCode.Should().Be(HttpStatusCode.OK);
        var orderId = await CheckoutAsync(customer, setup.Product.Id, checkoutOnly: true);
        var payment = await StartAsync(customer, orderId);
        (await customer.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemKycStates.Where(x => x.OrderItem.OrderId == orderId).ToListAsync()).Should().ContainSingle(x => x.Status == (byte)OrderItemKycStatus.AwaitingSubmission);
        (await verify.Carts.AnyAsync(x => x.GuestTokenHash == GuestCartToken.Hash(guestToken))).Should().BeFalse();
    }

    [Fact]
    public async Task Required_stage_two_input_remains_a_server_side_checkout_blocker()
    {
        var (customer, token) = await _fixture.CreateUserAndTokenAsync("Customer");
        var setup = await CreateProductAsync(DeliveryType.Manual, 1_000m, KycRequirementMode.Always, null);
        await using (var db = _fixture.CreateDbContext())
        {
            db.ProductInputFields.Add(new ProductInputField { Id = Guid.NewGuid(), ProductId = setup.Product.Id, Key = "p2g_required", Label = "Required", FieldType = (byte)ProductInputFieldType.Text, IsRequired = true, DisplayStage = (byte)ProductInputStage.Checkout, IsActive = true, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        using var client = _fixture.CreateClient(token);
        var add = await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = setup.Product.Id, Quantity = 1 });
        add.StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"p2g-stage2-{Guid.NewGuid():N}");
        (await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto())).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.CountAsync(x => x.UserId == customer.Id)).Should().Be(0);
        (await verify.Payments.CountAsync(x => x.Order.UserId == customer.Id)).Should().Be(0);
        var cart = (await add.Content.ReadFromJsonAsync<ApiResult<CartDto>>())!.Data!;
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        (await client.PutAsJsonAsync($"/api/cart/items/{cart.Items.Single().Id}", new UpdateCartItemRequestDto { Quantity = 1, InputValues = new Dictionary<string, string?> { ["p2g_required"] = "present" } })).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"p2g-stage2-valid-{Guid.NewGuid():N}");
        (await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    private async Task<Guid> CheckoutAndPayAsync(string token, Guid productId, int quantity = 1)
    {
        using var client = _fixture.CreateClient(token);
        var orderId = await CheckoutAsync(client, productId, quantity);
        var payment = await StartAsync(client, orderId);
        (await client.PostAsync($"/api/payments/mock/verify/{payment.PaymentId}", null)).StatusCode.Should().Be(HttpStatusCode.OK);
        return orderId;
    }

    private async Task<Guid> CheckoutAsync(HttpClient client, Guid productId, int quantity = 1, string? couponCode = null, bool checkoutOnly = false)
    {
        if (!checkoutOnly)
            (await client.PostAsJsonAsync("/api/cart/items", new AddToCartRequestDto { ProductId = productId, Quantity = quantity })).StatusCode.Should().Be(HttpStatusCode.OK);
        client.DefaultRequestHeaders.Remove("Idempotency-Key");
        client.DefaultRequestHeaders.Add("Idempotency-Key", $"p2g-checkout-{Guid.NewGuid():N}");
        var response = await client.PostAsJsonAsync("/api/checkout", new CheckoutRequestDto { CouponCode = couponCode });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<CheckoutResultDto>>())!.Data!.OrderId;
    }

    private async Task<PaymentStartResultDto> StartAsync(HttpClient client, Guid orderId, bool retry = false)
    {
        var response = await client.PostAsync($"/api/payments/{(retry ? "retry" : "start")}/{orderId}", null);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<PaymentStartResultDto>>())!.Data!;
    }

    private async Task<ProductSetup> CreateProductAsync(DeliveryType deliveryType, decimal price, KycRequirementMode mode, decimal? threshold, int giftCodes = 0, bool requiresDocument = false, KycSetup? setup = null)
    {
        setup ??= await CreateKycSetupAsync(requiresDocument);
        var now = DateTime.UtcNow;
        var category = new Category { Id = Guid.NewGuid(), Title = "Phase2G", Slug = $"p2g-{Guid.NewGuid():N}", IsActive = true, CreatedAt = now };
        var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = $"Phase2G {deliveryType}", Slug = $"p2g-product-{Guid.NewGuid():N}", ProductType = (byte)ProductType.GiftCard, DeliveryType = (byte)deliveryType, BasePrice = price, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1, IsActive = true, CreatedAt = now, RequiresVerification = mode != KycRequirementMode.None, KycRequirementMode = (byte)mode, KycThresholdAmount = threshold, KycPolicyVersionId = mode == KycRequirementMode.None ? null : setup.PolicyVersion.Id };
        await using var db = _fixture.CreateDbContext();
        db.AddRange(category, product);
        if (deliveryType == DeliveryType.Instant)
        {
            using var scope = _fixture.Factory.Services.CreateScope();
            var crypto = scope.ServiceProvider.GetRequiredService<IEncryptionService>();
            for (var i = 0; i < giftCodes; i++)
            {
                var secret = $"P2G-{Guid.NewGuid():N}";
                db.GiftCodes.Add(new GiftCode { Id = Guid.NewGuid(), ProductId = product.Id, EncryptedCode = crypto.Encrypt(secret), MaskedCode = "****P2G", CodeHashFingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(secret))), EncryptionVersion = 2, Status = (byte)GiftCodeStatus.Available, CreatedAt = now });
            }
        }
        await db.SaveChangesAsync();
        return new ProductSetup(product, setup.PolicyVersion, setup.DocumentType);
    }

    private async Task<KycSetup> CreateKycSetupAsync(bool requiresDocument)
    {
        var now = DateTime.UtcNow;
        var policy = new KycPolicy { Id = Guid.NewGuid(), Code = $"p2g-{Guid.NewGuid():N}", Name = "Phase2G", IsActive = true, CreatedAt = now };
        var version = new KycPolicyVersion { Id = Guid.NewGuid(), KycPolicyId = policy.Id, Version = 1, Status = (byte)KycPolicyVersionStatus.Published, CustomerTitle = "Phase2G", CreatedAt = now, PublishedAt = now };
        policy.Versions.Add(version);
        KycDocumentType? document = requiresDocument ? new KycDocumentType { Id = Guid.NewGuid(), Code = $"p2g-doc-{Guid.NewGuid():N}", Title = "Identity", IsActive = true, CreatedAt = now } : null;
        await using var db = _fixture.CreateDbContext();
        db.KycPolicies.Add(policy);
        if (document is not null)
            db.AddRange(document, new KycPolicyDocumentRequirement { Id = Guid.NewGuid(), KycPolicyVersionId = version.Id, KycDocumentTypeId = document.Id, IsRequired = true });
        await db.SaveChangesAsync();
        return new KycSetup(version, document);
    }

    private async Task<Coupon> CreateCouponAsync(decimal discount, decimal minimum)
    {
        var coupon = new Coupon { Id = Guid.NewGuid(), Code = $"P2G{Guid.NewGuid():N}", Title = "Phase2G", DiscountType = (byte)DiscountType.FixedAmount, DiscountValue = discount, MaxUsageCount = 10, MaxUsagePerUser = 1, MinOrderAmount = minimum, IsActive = true, StartsAt = DateTime.UtcNow.AddMinutes(-1), EndsAt = DateTime.UtcNow.AddHours(1), CreatedAt = DateTime.UtcNow };
        await using var db = _fixture.CreateDbContext(); db.Coupons.Add(coupon); await db.SaveChangesAsync(); return coupon;
    }

    private async Task AddWalletAsync(Guid userId, decimal balance)
    {
        await using var db = _fixture.CreateDbContext(); db.Wallets.Add(new Wallet { Id = Guid.NewGuid(), UserId = userId, Balance = balance, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
    }

    private async Task SetVerificationStatusAsync(Guid userId, VerificationStatus status)
    {
        await using var db = _fixture.CreateDbContext(); (await db.Users.SingleAsync(x => x.Id == userId)).VerificationStatus = (byte)status; await db.SaveChangesAsync();
    }

    private async Task<Guid> SubmitForReviewAsync(Guid userId, KycDocumentType document)
    {
        Guid orderItemId;
        await using (var db = _fixture.CreateDbContext())
        {
            orderItemId = await db.OrderItems
                .Where(x => x.Order.UserId == userId && x.KycPolicyVersionId != null)
                .Join(db.KycPolicyDocumentRequirements,
                    item => item.KycPolicyVersionId,
                    requirement => (Guid?)requirement.KycPolicyVersionId,
                    (item, requirement) => new { item.Id, requirement.KycDocumentTypeId })
                .Where(x => x.KycDocumentTypeId == document.Id)
                .Select(x => x.Id)
                .FirstAsync();
        }
        using var scope = _fixture.Factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IVerificationService>();
        var profile = await service.SubmitAsync(userId, new SubmitVerificationRequestDto { FirstName = "Phase", LastName = "TwoG", NationalCode = "1234567890" });
        await service.AddDocumentAsync(userId, 1, $"kyc-private:{userId:N}/{Guid.NewGuid():N}.jpg", document.Id, orderItemId);
        await service.SubmitAsync(userId, new SubmitVerificationRequestDto { FirstName = "Phase", LastName = "TwoG", NationalCode = "1234567890" });
        return profile.Id;
    }

    private async Task SubmitAndApproveAsync(Guid userId, Guid adminId, KycDocumentType document)
    {
        var profileId = await SubmitForReviewAsync(userId, document);
        using var scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IVerificationService>().ReviewAsync(profileId, adminId, new ReviewVerificationRequestDto { Approve = true });
    }

    private sealed record KycSetup(KycPolicyVersion PolicyVersion, KycDocumentType? DocumentType);
    private sealed record ProductSetup(Product Product, KycPolicyVersion PolicyVersion, KycDocumentType? DocumentType);
}
