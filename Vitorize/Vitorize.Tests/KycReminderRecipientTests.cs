using Microsoft.EntityFrameworkCore;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Infrastructure.Services;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

public sealed class KycReminderRecipientTests
{
    [Fact]
    public async Task Only_active_customers_with_paid_outstanding_kyc_orders_are_selectable()
    {
        await using var db = new VitorizeDbContext(new DbContextOptionsBuilder<VitorizeDbContext>()
            .UseInMemoryDatabase($"kyc-reminder-{Guid.NewGuid():N}").Options);
        var customer = new Role { Id = Guid.NewGuid(), Name = "Customer", DisplayName = "مشتری", CreatedAt = DateTime.UtcNow };

        var eligible = User("نیازمند احراز", VerificationStatus.Pending, customer);
        eligible.Orders.Add(Order(eligible.Id, "VT-ELIGIBLE", PaymentStatus.Paid, requiresVerification: true));

        var verified = User("تأییدشده", VerificationStatus.Verified, customer);
        verified.Orders.Add(Order(verified.Id, "VT-VERIFIED", PaymentStatus.Paid, requiresVerification: true));

        var unpaid = User("پرداخت نشده", VerificationStatus.Pending, customer);
        unpaid.Orders.Add(Order(unpaid.Id, "VT-UNPAID", PaymentStatus.Pending, requiresVerification: true));

        var notRequired = User("غیرمشمول", VerificationStatus.Pending, customer);
        notRequired.Orders.Add(Order(notRequired.Id, "VT-NO-KYC", PaymentStatus.Paid, requiresVerification: false));

        await db.Users.AddRangeAsync(eligible, verified, unpaid, notRequired);
        await db.SaveChangesAsync();

        var recipients = await new AdminNotificationReadService(db).GetKycReminderRecipientsAsync();

        var recipient = Assert.Single(recipients);
        Assert.Equal(eligible.Id, recipient.UserId);
        Assert.Equal("VT-ELIGIBLE", recipient.OrderNumber);
    }

    private static User User(string name, VerificationStatus verificationStatus, Role customerRole) => new()
    {
        Id = Guid.NewGuid(), FullName = name, Mobile = $"0912{Random.Shared.Next(1000000, 9999999)}",
        PasswordHash = "test", Status = (byte)UserStatus.Active,
        VerificationStatus = (byte)verificationStatus, CreatedAt = DateTime.UtcNow,
        Roles = new List<Role> { customerRole }
    };

    private static Order Order(Guid userId, string number, PaymentStatus paymentStatus, bool requiresVerification)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(), UserId = userId, OrderNumber = number,
            Status = (byte)OrderStatus.Processing, PaymentStatus = (byte)paymentStatus,
            CurrencyType = (byte)CurrencyType.Toman, CreatedAt = DateTime.UtcNow
        };
        order.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ProductId = Guid.NewGuid(), ProductTitle = "test",
            Quantity = 1, UnitPrice = 1, TotalPrice = 1, CurrencyType = (byte)CurrencyType.Toman,
            DeliveryType = (byte)DeliveryType.Manual, DeliveryStatus = (byte)DeliveryStatus.Pending,
            RequiresVerification = requiresVerification, CreatedAt = DateTime.UtcNow
        });
        return order;
    }
}
