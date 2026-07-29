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

    private static VitorizeDbContext CreateDb() => new(new DbContextOptionsBuilder<VitorizeDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static OrderService CreateService(VitorizeDbContext db) => new(
        db,
        Substitute.For<INotificationService>(),
        Substitute.For<IEncryptionService>());
}
