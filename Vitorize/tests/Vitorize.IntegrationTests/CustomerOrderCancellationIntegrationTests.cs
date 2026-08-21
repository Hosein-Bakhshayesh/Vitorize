using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.DTOs.Cart;
using Vitorize.Application.DTOs.Checkout;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;

namespace Vitorize.IntegrationTests;

/// <summary>
/// Customer cancellation of an unpaid order, against the real database and the real services.
///
/// Two things are being proved: that the customer can only act while the order is genuinely theirs to
/// act on, and that acting never damages financial state. Nothing is deleted by a cancellation - the
/// order, its number, its payment attempts and its status history all survive, which is why every
/// test here inspects the rows afterwards rather than only the return value.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class CustomerOrderCancellationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public CustomerOrderCancellationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    // ---------------------------------------------------------------- A: the happy path

    [Fact]
    public async Task An_unpaid_order_with_no_successful_payment_can_be_cancelled_by_its_owner()
    {
        var (user, order) = await PlaceOrderAsync();

        var result = await Orders().CancelMyOrderAsync(user.Id, order.Id);

        result.Status.Should().Be((byte)OrderStatus.Cancelled);
        await using var db = _fixture.CreateDbContext();
        var stored = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        stored.Status.Should().Be((byte)OrderStatus.Cancelled);
        stored.PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
    }

    [Fact]
    public async Task Cancelling_preserves_the_order_number_and_writes_an_auditable_history_row()
    {
        var (user, order) = await PlaceOrderAsync();
        var numberBefore = order.OrderNumber;

        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        await using var db = _fixture.CreateDbContext();
        var stored = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        stored.OrderNumber.Should().Be(numberBefore, "order history and accounting reference this number");

        var history = await db.OrderStatusHistories.AsNoTracking()
            .Where(x => x.OrderId == order.Id).ToListAsync();
        history.Should().ContainSingle(x =>
            x.ToStatus == (byte)OrderStatus.Cancelled && x.ChangedByUserId == user.Id);

        (await db.FinancialAuditLogs.AsNoTracking()
            .CountAsync(x => x.EntityId == order.Id && x.EventType == "OrderCancelledByCustomer"))
            .Should().Be(1);
    }

    // ---------------------------------------------------------------- B: ownership

    [Fact]
    public async Task Another_customer_cannot_cancel_an_order_they_do_not_own()
    {
        var (_, order) = await PlaceOrderAsync();
        var (attacker, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        // Ownership is part of the lookup, so an order belonging to somebody else is
        // indistinguishable from one that does not exist: the caller learns nothing either way.
        var act = () => Orders().CancelMyOrderAsync(attacker.Id, order.Id);
        await act.Should().ThrowAsync<NotFoundException>();

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id))
            .Status.Should().Be((byte)OrderStatus.PendingPayment, "the order was not touched");
    }

    [Fact]
    public async Task Another_customer_cannot_hide_an_order_they_do_not_own()
    {
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);
        var (attacker, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        var act = () => Orders().HideMyOrderAsync(attacker.Id, order.Id);
        await act.Should().ThrowAsync<NotFoundException>();

        await using var db = _fixture.CreateDbContext();
        (await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id))
            .HiddenByCustomerAt.Should().BeNull();
    }

    // ---------------------------------------------------------------- C and D: paid and delivered

    [Fact]
    public async Task A_paid_order_cannot_be_cancelled_by_the_customer()
    {
        var (user, order) = await PlaceOrderAsync();
        await MarkPaidAsync(order.Id);

        var act = () => Orders().CancelMyOrderAsync(user.Id, order.Id);
        (await act.Should().ThrowAsync<BusinessException>()).And.Message.Should().Contain("پرداخت");
    }

    [Fact]
    public async Task A_delivered_order_cannot_be_cancelled_by_the_customer()
    {
        var (user, order) = await PlaceOrderAsync();
        await MarkPaidAsync(order.Id);
        await using (var db = _fixture.CreateDbContext())
        {
            foreach (var item in await db.OrderItems.Where(x => x.OrderId == order.Id).ToListAsync())
            {
                item.DeliveryStatus = (byte)DeliveryStatus.Delivered;
                item.DeliveredAt = DateTime.UtcNow;
            }
            var completed = await db.Orders.SingleAsync(x => x.Id == order.Id);
            completed.Status = (byte)OrderStatus.Completed;
            await db.SaveChangesAsync();
        }

        var act = () => Orders().CancelMyOrderAsync(user.Id, order.Id);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task An_order_whose_fulfillment_started_cannot_be_cancelled_even_while_unpaid()
    {
        var (user, order) = await PlaceOrderAsync();
        await using (var db = _fixture.CreateDbContext())
        {
            var item = await db.OrderItems.FirstAsync(x => x.OrderId == order.Id);
            item.DeliveryStatus = (byte)DeliveryStatus.ManualReview;
            await db.SaveChangesAsync();
        }

        var act = () => Orders().CancelMyOrderAsync(user.Id, order.Id);
        (await act.Should().ThrowAsync<BusinessException>()).And.Message.Should().Contain("تحویل");
    }

    // ---------------------------------------------------------------- E and F: after cancellation

    [Fact]
    public async Task A_cancelled_order_cannot_be_paid_again_by_any_route()
    {
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        using var scope = _fixture.Factory.Services.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();

        (await payments.GetRetryEligibilityAsync(user.Id, order.Id)).CanRetry.Should().BeFalse();
        await ((Func<Task>)(() => payments.StartPaymentAsync(user.Id, order.Id)))
            .Should().ThrowAsync<BusinessException>();
        // Wallet is an internal debit with no provider session, so it is refused by state.
        await ((Func<Task>)(() => payments.PayWithWalletAsync(user.Id, order.Id)))
            .Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Cancelling_never_starts_fulfillment_and_never_consumes_stock()
    {
        var (user, order) = await PlaceOrderAsync();
        var stockBefore = await VariantStockAsync(order.Id);

        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        await using var db = _fixture.CreateDbContext();
        (await VariantStockAsync(order.Id)).Should().Be(stockBefore, "stock is only consumed on the paid transition");
        (await db.OrderItemDeliveries.CountAsync(x => x.OrderItem.OrderId == order.Id)).Should().Be(0);
        (await db.OrderItems.Where(x => x.OrderId == order.Id)
            .AllAsync(x => x.DeliveryStatus == (byte)DeliveryStatus.Pending)).Should().BeTrue();
    }

    [Fact]
    public async Task Cancelling_releases_a_reserved_gift_code_back_to_the_pool()
    {
        var (user, order) = await PlaceOrderAsync();
        var giftCode = await ReserveGiftCodeAsync(order, user.Id);

        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        await using var db = _fixture.CreateDbContext();
        (await db.GiftCodes.AsNoTracking().SingleAsync(x => x.Id == giftCode))
            .Status.Should().Be((byte)GiftCodeStatus.Available, "an abandoned reservation must return to stock");
        (await db.GiftCodeReservations.AsNoTracking().SingleAsync(x => x.GiftCodeId == giftCode))
            .Status.Should().Be((byte)GiftCodeReservationStatus.Released);
    }

    // ---------------------------------------------------------------- H: repeat calls

    [Fact]
    public async Task Cancelling_twice_is_rejected_as_a_business_rule_and_changes_nothing()
    {
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        var act = () => Orders().CancelMyOrderAsync(user.Id, order.Id);
        await act.Should().ThrowAsync<BusinessException>();

        await using var db = _fixture.CreateDbContext();
        (await db.OrderStatusHistories.CountAsync(x =>
            x.OrderId == order.Id && x.ToStatus == (byte)OrderStatus.Cancelled))
            .Should().Be(1, "a rejected second call must not append another history row");
    }

    // ---------------------------------------------------------------- I: settlement race

    [Fact]
    public async Task An_order_with_a_live_gateway_session_is_not_cancellable()
    {
        var (user, order) = await PlaceOrderAsync();

        using var scope = _fixture.Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPaymentService>().StartPaymentAsync(user.Id, order.Id);

        // The customer can still complete this session at the bank, and there is no provider void,
        // so cancelling now could produce an order that is both cancelled and paid.
        var act = () => Orders().CancelMyOrderAsync(user.Id, order.Id);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task A_success_that_arrives_after_cancellation_is_never_fulfilled_and_is_flagged_for_finance()
    {
        // The containment half of the race. The cancellability rule above stops this from happening
        // through the UI; this proves that if it ever does happen, the money is not silently
        // converted into goods.
        var (user, order) = await PlaceOrderAsync();
        using var scope = _fixture.Factory.Services.CreateScope();
        var payments = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        var started = await payments.StartPaymentAsync(user.Id, order.Id);

        // Force the order into the cancelled state behind the attempt's back.
        await using (var db = _fixture.CreateDbContext())
        {
            var stored = await db.Orders.SingleAsync(x => x.Id == order.Id);
            stored.Status = (byte)OrderStatus.Cancelled;
            await db.SaveChangesAsync();
        }

        var verify = await payments.VerifyZarinpalPaymentAsync(started.Authority!, "OK");

        verify.IsPaid.Should().BeFalse("a cancelled order must not be fulfilled by a late success");
        await using var check = _fixture.CreateDbContext();
        var reread = await check.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id);
        reread.Status.Should().Be((byte)OrderStatus.Cancelled);
        reread.PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);

        var payment = await check.Payments.AsNoTracking().SingleAsync(x => x.Id == started.PaymentId);
        payment.ProviderStatusCode.Should().Be("LATE_SUCCESS_ON_CANCELLED_ORDER_REQUIRES_FINANCE");
        payment.CallbackVerified.Should().BeTrue("the gateway proof is preserved for the refund");
        (await check.FinancialAuditLogs.AsNoTracking().CountAsync(x =>
            x.EntityId == payment.Id && x.EventType == "LateGatewayPaymentRequiresFinanceResolution"))
            .Should().Be(1);
        (await check.OrderItemDeliveries.CountAsync(x => x.OrderItem.OrderId == order.Id)).Should().Be(0);
    }

    // ---------------------------------------------------------------- J and K: projection

    [Fact]
    public async Task The_list_and_the_details_projection_agree_on_the_customer_actions()
    {
        var (user, order) = await PlaceOrderAsync();

        var fromList = (await Orders().GetMyOrdersAsync(user.Id)).Single(x => x.Id == order.Id);
        var fromDetails = await Orders().GetMyOrderDetailsAsync(user.Id, order.Id);

        fromList.CanCustomerCancel.Should().BeTrue();
        fromDetails.CanCustomerCancel.Should().Be(fromList.CanCustomerCancel);
        fromDetails.CanCustomerHide.Should().Be(fromList.CanCustomerHide);
        fromList.Status.Should().Be(fromDetails.Status);
        fromList.PaymentStatus.Should().Be(fromDetails.PaymentStatus);
    }

    [Fact]
    public async Task An_unpaid_order_never_reports_a_delivery_status_other_than_pending()
    {
        // The presentation layer gates on this: if an unpaid order could report anything but Pending,
        // the customer could legitimately be shown delivery progress.
        var (user, order) = await PlaceOrderAsync();

        var details = await Orders().GetMyOrderDetailsAsync(user.Id, order.Id);

        details.PaymentStatus.Should().NotBe((byte)PaymentStatus.Paid);
        details.Items.Should().OnlyContain(x => x.DeliveryStatus == (byte)DeliveryStatus.Pending);
        details.Items.Should().OnlyContain(x => x.Deliveries.Count == 0);
    }

    [Fact]
    public async Task The_admin_projection_offers_no_customer_actions()
    {
        var (_, order) = await PlaceOrderAsync();

        var admin = await Orders().GetAdminOrderDetailsAsync(order.Id);

        admin.CanCustomerCancel.Should().BeFalse("these are the customer actions, not administrative ones");
        admin.CanCustomerHide.Should().BeFalse();
    }

    // ---------------------------------------------------------------- hiding

    [Fact]
    public async Task A_cancelled_order_can_be_hidden_and_disappears_only_from_the_customer_list()
    {
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);
        var paymentsBefore = await PaymentCountAsync(order.Id);

        await Orders().HideMyOrderAsync(user.Id, order.Id);

        (await Orders().GetMyOrdersAsync(user.Id)).Should().NotContain(x => x.Id == order.Id);
        // Admin still sees it, and the row is untouched apart from the visibility stamp.
        (await Orders().GetAdminOrdersAsync()).Should().Contain(x => x.Id == order.Id);
        await using var db = _fixture.CreateDbContext();
        (await db.Orders.AsNoTracking().SingleAsync(x => x.Id == order.Id))
            .HiddenByCustomerAt.Should().NotBeNull();
        (await db.Payments.CountAsync(x => x.OrderId == order.Id))
            .Should().Be(paymentsBefore, "payment history is not deleted by hiding");
    }

    [Fact]
    public async Task An_order_still_awaiting_payment_cannot_be_hidden()
    {
        var (user, order) = await PlaceOrderAsync();

        var act = () => Orders().HideMyOrderAsync(user.Id, order.Id);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task Hiding_twice_is_idempotent()
    {
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);

        await Orders().HideMyOrderAsync(user.Id, order.Id);
        var act = () => Orders().HideMyOrderAsync(user.Id, order.Id);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task A_hidden_order_is_still_reachable_by_its_owner_through_a_direct_link()
    {
        // Hiding is a list preference, not a revocation of access to the customer's own record.
        var (user, order) = await PlaceOrderAsync();
        await Orders().CancelMyOrderAsync(user.Id, order.Id);
        await Orders().HideMyOrderAsync(user.Id, order.Id);

        var details = await Orders().GetMyOrderDetailsAsync(user.Id, order.Id);

        details.Id.Should().Be(order.Id);
    }

    // ---------------------------------------------------------------- helpers

    private IOrderService Orders() =>
        _fixture.Factory.Services.CreateScope().ServiceProvider.GetRequiredService<IOrderService>();

    private async Task<(User User, Order Order)> PlaceOrderAsync()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var product = await SeedProductAsync();

        await using (var db = _fixture.CreateDbContext())
        {
            var cart = new CartService(db,
                _fixture.Factory.Services.GetRequiredService<IEncryptionService>(),
                new VatSettingsProvider(db));
            await cart.AddItemAsync(user.Id, new AddToCartRequestDto { ProductId = product.Id, Quantity = 1 });
        }

        using var scope = _fixture.Factory.Services.CreateScope();
        var checkout = await scope.ServiceProvider.GetRequiredService<ICheckoutService>()
            .CheckoutAsync(user.Id, new CheckoutRequestDto());

        await using var verify = _fixture.CreateDbContext();
        return (user, await verify.Orders.AsNoTracking().SingleAsync(x => x.Id == checkout.OrderId));
    }

    private async Task<Product> SeedProductAsync(int stock = 10)
    {
        await using var db = _fixture.CreateDbContext();
        var category = new Category
        {
            Id = Guid.NewGuid(), Title = "cancel", Slug = $"cancel-{Guid.NewGuid():N}",
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        var product = new Product
        {
            Id = Guid.NewGuid(), CategoryId = category.Id, Title = "Cancellable product",
            Slug = $"cancel-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)DeliveryType.Manual,
            BasePrice = 25_000m, CurrencyType = (byte)CurrencyType.Toman, MinOrderQuantity = 1,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };
        product.WithCanonicalVariant(stock);
        db.Categories.Add(category);
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private async Task MarkPaidAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        var order = await db.Orders.SingleAsync(x => x.Id == orderId);
        order.PaymentStatus = (byte)PaymentStatus.Paid;
        order.Status = (byte)OrderStatus.Processing;
        order.PaidAt = DateTime.UtcNow;
        db.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(), OrderId = orderId, UserId = order.UserId, Amount = order.FinalAmount,
            CurrencyType = order.CurrencyType, Gateway = "Zarinpal", Status = (byte)PaymentStatus.Paid,
            RequestedAt = DateTime.UtcNow, VerifiedAt = DateTime.UtcNow, CallbackVerified = true
        });
        await db.SaveChangesAsync();
    }

    private async Task<Guid> ReserveGiftCodeAsync(Order order, Guid userId)
    {
        await using var db = _fixture.CreateDbContext();
        var item = await db.OrderItems.AsNoTracking().FirstAsync(x => x.OrderId == order.Id);
        var code = new GiftCode
        {
            Id = Guid.NewGuid(), ProductId = item.ProductId, ProductVariantId = item.ProductVariantId,
            EncryptedCode = $"enc-{Guid.NewGuid():N}", MaskedCode = "GC-****",
            EncryptionVersion = 1, Status = (byte)GiftCodeStatus.Reserved,
            ReservedByUserId = userId, ReservedAt = DateTime.UtcNow,
            ReservationExpiresAt = DateTime.UtcNow.AddMinutes(30), CreatedAt = DateTime.UtcNow
        };
        db.GiftCodes.Add(code);
        db.GiftCodeReservations.Add(new GiftCodeReservation
        {
            Id = Guid.NewGuid(), OrderId = order.Id, GiftCodeId = code.Id, UserId = userId,
            ProductId = item.ProductId, ProductVariantId = item.ProductVariantId,
            Status = (byte)GiftCodeReservationStatus.Active, ReservedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddMinutes(30)
        });
        await db.SaveChangesAsync();
        return code.Id;
    }

    private async Task<int> VariantStockAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        var variantId = await db.OrderItems.AsNoTracking()
            .Where(x => x.OrderId == orderId).Select(x => x.ProductVariantId).FirstAsync();
        return variantId is null
            ? 0
            : await db.ProductVariants.AsNoTracking().Where(x => x.Id == variantId)
                .Select(x => x.StockQuantity).SingleAsync();
    }

    private async Task<int> PaymentCountAsync(Guid orderId)
    {
        await using var db = _fixture.CreateDbContext();
        return await db.Payments.CountAsync(x => x.OrderId == orderId);
    }
}
