using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

public sealed class OrderLifecycleHardeningTests
{
    [Fact]
    public async Task Paid_order_cannot_be_cancelled_outside_the_refund_workflow()
    {
        await using var db = CreateDb();
        db.Orders.Add(new Order
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), OrderNumber = "VT-PAID",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.CancelOrderAsync(db.Orders.Single().Id, Guid.NewGuid(), "operator request"));

        db.Orders.Single().Status.Should().Be((byte)OrderStatus.Processing);
    }

    [Fact]
    public async Task Paid_order_with_undelivered_items_cannot_be_completed()
    {
        await using var db = CreateDb();
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = Guid.NewGuid(), OrderNumber = "VT-DELIVERY",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid,
            CreatedAt = DateTime.UtcNow
        };
        order.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = Guid.NewGuid(), ProductTitle = "Manual item",
            Quantity = 1, UnitPrice = 1, TotalPrice = 1, DeliveryType = (byte)DeliveryType.Manual,
            DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
        });
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var service = CreateService(db);

        await Assert.ThrowsAsync<BusinessException>(() =>
            service.CompleteOrderAsync(order.Id, Guid.NewGuid()));

        db.Orders.Single().Status.Should().Be((byte)OrderStatus.Processing);
    }

    [Fact]
    public async Task Order_detail_input_values_follow_the_product_field_sort_order()
    {
        await using var db = CreateDb();
        var productId = Guid.NewGuid();
        var early = new ProductInputField { Id = Guid.NewGuid(), ProductId = productId, Key = "rules", Label = "تأیید قوانین", SortOrder = 10, IsActive = true, CreatedAt = DateTime.UtcNow };
        var middle = new ProductInputField { Id = Guid.NewGuid(), ProductId = productId, Key = "account", Label = "شناسه اکانت", SortOrder = 20, IsActive = true, CreatedAt = DateTime.UtcNow };
        var late = new ProductInputField { Id = Guid.NewGuid(), ProductId = productId, Key = "note", Label = "توضیحات", SortOrder = 30, IsActive = true, CreatedAt = DateTime.UtcNow };
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "مدیر آزمون", Mobile = "09120000000", PasswordHash = "hash",
            Status = (byte)UserStatus.Active, IsMobileConfirmed = true, CreatedAt = DateTime.UtcNow
        };
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = user.Id, User = user, OrderNumber = "VT-INPUT-ORDER",
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)PaymentStatus.Paid, CreatedAt = DateTime.UtcNow
        };
        var item = new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = productId, ProductTitle = "محصول آزمون",
            Quantity = 1, UnitPrice = 1, TotalPrice = 1, DeliveryType = (byte)DeliveryType.Manual,
            DeliveryStatus = (byte)DeliveryStatus.Pending, CreatedAt = DateTime.UtcNow
        };
        // Deliberately add the saved values in a different order from the product editor.
        item.InputValues.Add(new OrderItemInputValue { Id = Guid.NewGuid(), OrderItemId = item.Id, ProductInputFieldId = late.Id, ProductInputField = late, FieldKey = late.Key, FieldLabel = late.Label, FieldType = 1, Value = "یادداشت", CreatedAt = DateTime.UtcNow });
        item.InputValues.Add(new OrderItemInputValue { Id = Guid.NewGuid(), OrderItemId = item.Id, ProductInputFieldId = early.Id, ProductInputField = early, FieldKey = early.Key, FieldLabel = early.Label, FieldType = 4, Value = "true", CreatedAt = DateTime.UtcNow });
        item.InputValues.Add(new OrderItemInputValue { Id = Guid.NewGuid(), OrderItemId = item.Id, ProductInputFieldId = middle.Id, ProductInputField = middle, FieldKey = middle.Key, FieldLabel = middle.Label, FieldType = 1, Value = "player-1", CreatedAt = DateTime.UtcNow });
        order.OrderItems.Add(item);
        db.Orders.Add(order);
        await db.SaveChangesAsync();

        var detail = await CreateService(db).GetAdminOrderDetailsAsync(order.Id);

        detail.Items.Single().InputValues.Select(value => value.FieldKey)
            .Should().Equal("rules", "account", "note");
    }

    private static VitorizeDbContext CreateDb() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OrderService CreateService(VitorizeDbContext db) => new(
        db,
        Substitute.For<INotificationService>(),
        Substitute.For<IEncryptionService>());
}
