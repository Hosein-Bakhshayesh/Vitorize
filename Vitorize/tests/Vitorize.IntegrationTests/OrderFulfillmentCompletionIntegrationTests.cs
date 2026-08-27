using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Vitorize.Application.Interfaces;
using Vitorize.Application.DTOs.Orders;
using Vitorize.Application.DTOs.Admin.Orders;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Services;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Common;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class OrderFulfillmentCompletionIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;
    public OrderFulfillmentCompletionIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Completion_requires_paid_and_every_item_delivered()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (order, pending, delivered) = await SeedOrderAsync(user.Id, DeliveryType.Manual, DeliveryType.Instant);
        await using (var db = _fixture.CreateDbContext())
        {
            var service = new OrderService(db, new NullNotifications(), Crypto());
            var act = () => service.CompleteOrderAsync(order.Id, admin.Id);
            (await act.Should().ThrowAsync<BusinessException>()).Which.Message.Should()
                .Be("تا زمان تحویل همه آیتم‌ها، تکمیل سفارش مجاز نیست.");
        }
        await using (var db = _fixture.CreateDbContext())
        {
            var item = await db.OrderItems.SingleAsync(x => x.Id == pending.Id);
            item.DeliveryStatus = (byte)DeliveryStatus.Delivered;
            item.DeliveredAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        await using (var db = _fixture.CreateDbContext())
            await new OrderService(db, new NullNotifications(), Crypto()).CompleteOrderAsync(order.Id, admin.Id);
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Completed);
        (await verify.OrderStatusHistories.CountAsync(x => x.OrderId == order.Id)).Should().Be(1);
    }

    /// <summary>
    /// SupportRequired is fulfilled through its own support workflow — the automatically created
    /// fulfilment ticket, where the administrator delivers the service and closes the thread. It must
    /// not borrow the Manual evidence route, so the order legitimately stays in Processing and the
    /// item never gains an OrderItemDelivery. FIX-09 Phase 2E asserts the same boundary over HTTP;
    /// this test previously asserted the pre-Phase-2E behaviour and contradicted it.
    /// </summary>
    [Fact]
    public async Task Support_required_is_refused_by_the_manual_route_and_keeps_its_own_workflow()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (order, support, _) = await SeedOrderAsync(user.Id, DeliveryType.SupportRequired);

        await using (var db = _fixture.CreateDbContext())
        {
            var service = new OrderService(db, new NullNotifications(), Crypto());
            var act = () => service.DeliverManualAsync(order.Id, admin.Id, new ManualDeliveryRequestDto
            {
                OrderItemId = support.Id, Content = "support fulfillment", IsVisibleToCustomer = true
            });
            (await act.Should().ThrowAsync<BusinessException>()).Which.Message.Should()
                .Be("این آیتم برای تحویل دستی تعریف نشده است.");
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == support.Id)).Should().Be(0,
            "no manual delivery evidence may be fabricated for a support item");
        (await verify.OrderItems.SingleAsync(x => x.Id == support.Id)).DeliveryStatus
            .Should().Be((byte)DeliveryStatus.Pending);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status
            .Should().Be((byte)OrderStatus.Processing, "a paid support order waits on its support workflow");
    }

    /// <summary>
    /// The manual evidence route belongs to Manual alone. Instant availability is the gift-code
    /// pool, so hand-delivering it would fabricate a delivery for a code that was never allocated.
    /// </summary>
    [Fact]
    public async Task Instant_items_can_never_be_fulfilled_by_hand()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (order, instant, _) = await SeedOrderAsync(user.Id, DeliveryType.Instant);

        await using (var db = _fixture.CreateDbContext())
        {
            var service = new OrderService(db, new NullNotifications(), Crypto());
            var act = () => service.DeliverManualAsync(order.Id, admin.Id, new ManualDeliveryRequestDto
            {
                OrderItemId = instant.Id, Content = "should never be accepted", IsVisibleToCustomer = true
            });
            (await act.Should().ThrowAsync<BusinessException>()).Which.Message.Should()
                .Be("این آیتم برای تحویل دستی تعریف نشده است.");
        }

        await using var verify = _fixture.CreateDbContext();
        (await verify.OrderItemDeliveries.CountAsync(x => x.OrderItemId == instant.Id)).Should().Be(0);
        (await verify.OrderItems.SingleAsync(x => x.Id == instant.Id)).DeliveryStatus
            .Should().Be((byte)DeliveryStatus.Pending);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status
            .Should().Be((byte)OrderStatus.Processing, "an unfulfillable item must not complete the order");
    }

    /// <summary>The positive case for the manual route, so the two refusals above cannot pass vacuously.</summary>
    [Fact]
    public async Task Manual_items_still_record_delivery_evidence_and_complete_the_order()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (admin, _) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (order, manual, _) = await SeedOrderAsync(user.Id, DeliveryType.Manual);

        await using (var db = _fixture.CreateDbContext())
            await new OrderService(db, new NullNotifications(), Crypto()).DeliverManualAsync(
                order.Id, admin.Id, new ManualDeliveryRequestDto
                {
                    OrderItemId = manual.Id, Content = "manual fulfillment", IsVisibleToCustomer = true
                });

        await using var verify = _fixture.CreateDbContext();
        var delivery = await verify.OrderItemDeliveries.SingleAsync(x => x.OrderItemId == manual.Id);
        delivery.DeliveryType.Should().Be((byte)DeliveryType.Manual, "the evidence records the item's own mode");
        (await verify.OrderItems.SingleAsync(x => x.Id == manual.Id)).DeliveryStatus
            .Should().Be((byte)DeliveryStatus.Delivered);
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Completed);
    }

    [Fact]
    public async Task Completion_api_rejects_a_paid_pending_item_with_the_specific_business_reason()
    {
        var (user, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        var (order, _, _) = await SeedOrderAsync(user.Id, DeliveryType.Manual);
        using var client = _fixture.CreateClient(adminToken);
        var response = await client.PostAsync($"/api/admin/orders/{order.Id}/complete", null);
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var result = await response.Content.ReadFromJsonAsync<ApiResult>();
        result!.Message.Should().Be("تا زمان تحویل همه آیتم‌ها، تکمیل سفارش مجاز نیست.");
        await using var verify = _fixture.CreateDbContext();
        (await verify.Orders.SingleAsync(x => x.Id == order.Id)).Status.Should().Be((byte)OrderStatus.Processing);
    }

    private IEncryptionService Crypto() => _fixture.Factory.Services.GetRequiredService<IEncryptionService>();

    private async Task<(Order Order, OrderItem First, OrderItem Second)> SeedOrderAsync(Guid userId, params DeliveryType[] types)
    {
        var category = new Category { Id = Guid.NewGuid(), Title = "Fulfillment", Slug = $"fulfillment-{Guid.NewGuid():N}", IsActive = true, CreatedAt = DateTime.UtcNow };
        var order = new Order { Id = Guid.NewGuid(), UserId = userId, OrderNumber = $"FUL-{Guid.NewGuid():N}", Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, CurrencyType = 2, CreatedAt = DateTime.UtcNow };
        var items = new List<OrderItem>();
        foreach (var type in types)
        {
            var product = new Product { Id = Guid.NewGuid(), CategoryId = category.Id, Title = type.ToString(), Slug = $"fulfillment-{Guid.NewGuid():N}", ProductType = 1, DeliveryType = (byte)type, BasePrice = 10, CurrencyType = 2, MinOrderQuantity = 1, IsActive = true, CreatedAt = DateTime.UtcNow };
            items.Add(new OrderItem { Id = Guid.NewGuid(), OrderId = order.Id, ProductId = product.Id, ProductTitle = product.Title, Quantity = 1, UnitPrice = 10, TotalPrice = 10, CurrencyType = 2, DeliveryType = (byte)type, DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow, Product = product });
        }
        if (items.Count > 1) items[1].DeliveryStatus = (byte)DeliveryStatus.Delivered;
        await using var db = _fixture.CreateDbContext();
        db.Categories.Add(category); db.Orders.Add(order); db.OrderItems.AddRange(items);
        await db.SaveChangesAsync();
        return (order, items[0], items.Count > 1 ? items[1] : items[0]);
    }

    private sealed class NullNotifications : INotificationService
    {
        public Task CreateAsync(Guid userId, byte type, string title, string message) => Task.CompletedTask;
        public Task SendSystemNotificationAsync(Guid userId, string title, string message, bool sendSms = false, Guid? smsCreatedByUserId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<int> CreateBulkAsync(Guid broadcastId, IReadOnlyCollection<Guid> recipientUserIds, string title, string message, bool sendSms = false, Guid? smsCreatedByUserId = null, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task MarkAsReadAsync(Guid userId, Guid notificationId) => Task.CompletedTask;
        public Task MarkAllAsReadAsync(Guid userId) => Task.CompletedTask;
        public Task<int> GetUnreadCountAsync(Guid userId) => Task.FromResult(0);
        public Task<List<Vitorize.Application.DTOs.Notifications.NotificationDto>> GetMyNotificationsAsync(Guid userId) => Task.FromResult(new List<Vitorize.Application.DTOs.Notifications.NotificationDto>());
    }
}
