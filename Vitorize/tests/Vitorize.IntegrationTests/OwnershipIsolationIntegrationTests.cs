using System.Net;
using FluentAssertions;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class OwnershipIsolationIntegrationTests
{
    private readonly IntegrationTestFixture _fixture;

    public OwnershipIsolationIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Customer_B_cannot_read_or_mutate_customer_A_resources()
    {
        var (owner, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (_, attackerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        var orderId = Guid.NewGuid();
        var notificationId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        await using (var db = _fixture.CreateDbContext())
        {
            db.Orders.Add(new Order
            {
                Id = orderId, UserId = owner.Id, OrderNumber = $"VT-IDOR-{Guid.NewGuid():N}",
                Status = (byte)OrderStatus.PendingPayment, PaymentStatus = (byte)PaymentStatus.Pending,
                CreatedAt = DateTime.UtcNow
            });
            db.Notifications.Add(new Notification
            {
                Id = notificationId, UserId = owner.Id, Title = "Owner only", Message = "private",
                Type = (byte)NotificationType.SystemMessage, CreatedAt = DateTime.UtcNow
            });
            db.Tickets.Add(new Ticket
            {
                Id = ticketId, UserId = owner.Id, Subject = "Owner ticket",
                Department = (byte)TicketDepartment.General,
                Priority = (byte)TicketPriority.Normal,
                Status = (byte)TicketStatus.WaitingForAdmin, CreatedAt = DateTime.UtcNow
            });
            var profile = new UserVerificationProfile
            {
                Id = Guid.NewGuid(), UserId = owner.Id, FirstName = "Test", LastName = "Owner",
                NationalCode = "0013546789", Status = (byte)VerificationStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
            profile.VerificationDocuments.Add(new VerificationDocument
            {
                Id = documentId, DocumentType = 1,
                FilePath = $"kyc-private:{owner.Id:N}/document.jpg",
                Status = (byte)VerificationStatus.Pending, CreatedAt = DateTime.UtcNow
            });
            db.UserVerificationProfiles.Add(profile);
            await db.SaveChangesAsync();
        }

        using var attacker = _fixture.CreateClient(attackerToken);
        (await attacker.GetAsync($"/api/orders/{orderId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await attacker.PostAsync($"/api/notifications/{notificationId}/read", null)).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
        (await attacker.GetAsync($"/api/tickets/{ticketId}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await attacker.GetAsync($"/api/verification/documents/{documentId}/content")).StatusCode
            .Should().Be(HttpStatusCode.NotFound);
    }
}
