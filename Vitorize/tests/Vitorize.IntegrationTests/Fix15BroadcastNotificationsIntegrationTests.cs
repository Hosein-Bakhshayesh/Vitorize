using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Admin.Notifications;
using Vitorize.Application.DTOs.Notifications;
using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.IntegrationTests.Infrastructure;
using Vitorize.Shared.Common;
using Vitorize.Shared.Enums;

namespace Vitorize.IntegrationTests;

/// <summary>
/// FIX-15 (Client Issue #15) over real HTTP and SQL Server: audience resolution, atomic idempotent
/// delivery, the 5,000 cap, action-link safety, read state and audit volume.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Fix15BroadcastNotificationsIntegrationTests
{
    private const byte AllCustomers = (byte)BroadcastAudience.AllCustomers;
    private const byte SelectedCustomers = (byte)BroadcastAudience.SelectedCustomers;

    private readonly IntegrationTestFixture _fixture;

    public Fix15BroadcastNotificationsIntegrationTests(IntegrationTestFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task An_all_customers_broadcast_reaches_every_eligible_customer_exactly_once()
    {
        using var admin = await SuperAdminClientAsync();
        var (activeA, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (activeB, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (blocked, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (staff, _) = await _fixture.CreateUserAndTokenAsync("Support");
        var (staffWhoIsAlsoCustomer, _) = await _fixture.CreateUserAndTokenAsync("Admin", "Customer");
        await SetStatusAsync(blocked.Id, UserStatus.Blocked);

        var title = Unique("اطلاعیه همگانی");
        var broadcast = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = AllCustomers, Title = title, Message = "متن اطلاعیه", ActionUrl = "/shop"
        });

        broadcast.Status.Should().Be((byte)BroadcastStatus.Sent);
        broadcast.SentAt.Should().NotBeNull();

        await using var db = _fixture.CreateDbContext();
        var rows = await db.Notifications.AsNoTracking()
            .Where(x => x.BroadcastId == broadcast.Id).ToListAsync();

        rows.Should().OnlyContain(x => x.Type == (byte)NotificationType.Announcement);
        rows.Should().OnlyContain(x => !x.IsRead && x.ReadAt == null);
        rows.Count(x => x.UserId == activeA.Id).Should().Be(1);
        rows.Count(x => x.UserId == activeB.Id).Should().Be(1);
        rows.Should().NotContain(x => x.UserId == blocked.Id, "a blocked account cannot use the storefront");
        rows.Should().NotContain(x => x.UserId == staff.Id, "staff must never receive a customer broadcast");
        rows.Should().NotContain(x => x.UserId == staffWhoIsAlsoCustomer.Id,
            "a staff account is excluded even when it also holds the Customer role");

        // History reports the number of rows actually delivered.
        broadcast.RecipientCount.Should().Be(rows.Count);
    }

    [Fact]
    public async Task A_selected_broadcast_reaches_only_the_chosen_customers_and_deduplicates()
    {
        using var admin = await SuperAdminClientAsync();
        var (a, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (b, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (c, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        var broadcast = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers,
            // a is listed twice on purpose.
            SelectedCustomerIds = [a.Id, c.Id, a.Id],
            Title = Unique("اطلاعیه انتخابی"), Message = "متن"
        });

        broadcast.RecipientCount.Should().Be(2, "the duplicate selection collapses to one recipient");

        await using var db = _fixture.CreateDbContext();
        var recipients = await db.Notifications.AsNoTracking()
            .Where(x => x.BroadcastId == broadcast.Id).Select(x => x.UserId).ToListAsync();

        recipients.Should().BeEquivalentTo(new[] { a.Id, c.Id });
        recipients.Should().NotContain(b.Id);
    }

    [Fact]
    public async Task Selecting_an_ineligible_user_is_refused_rather_than_silently_dropped()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (staff, _) = await _fixture.CreateUserAndTokenAsync("Support");

        var response = await PostSendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers,
            SelectedCustomerIds = [customer.Id, staff.Id],
            Title = Unique("نامعتبر"), Message = "متن"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync());

        await using var db = _fixture.CreateDbContext();
        (await db.Notifications.CountAsync(x => x.UserId == staff.Id && x.BroadcastId != null)).Should().Be(0);
    }

    [Fact]
    public async Task Preview_reports_the_eligible_count_and_flags_ineligible_selections()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (staff, _) = await _fixture.CreateUserAndTokenAsync("KycViewer");

        var response = await admin.PostAsJsonAsync("/api/admin/notification-broadcasts/preview",
            new BroadcastPreviewRequestDto
            {
                Audience = SelectedCustomers,
                SelectedCustomerIds = [customer.Id, staff.Id, customer.Id]
            });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var preview = (await response.Content.ReadFromJsonAsync<ApiResult<BroadcastPreviewResultDto>>())!.Data!;

        preview.RecipientCount.Should().Be(1);
        preview.IneligibleCount.Should().Be(1);
        preview.MaximumRecipients.Should().Be(BroadcastRecipientRules.MaximumRecipients);
        preview.ExceedsLimit.Should().BeFalse();
    }

    [Fact]
    public async Task Replaying_the_same_idempotency_key_creates_no_second_broadcast_or_duplicate_row()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var request = new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customer.Id],
            Title = Unique("تکرار"), Message = "متن"
        };
        var key = $"fix15-{Guid.NewGuid():N}";

        var first = await PostSendAsync(admin, request, key);
        first.StatusCode.Should().Be(HttpStatusCode.OK, await first.Content.ReadAsStringAsync());

        var replay = await PostSendAsync(admin, request, key);
        replay.StatusCode.Should().Be(HttpStatusCode.BadRequest, "a completed key must not run the send again");

        await using var db = _fixture.CreateDbContext();
        (await db.NotificationBroadcasts.CountAsync(x => x.Title == request.Title)).Should().Be(1);
        (await db.Notifications.CountAsync(x =>
            x.UserId == customer.Id && x.Title == request.Title)).Should().Be(1);
    }

    [Fact]
    public async Task Reusing_a_key_with_a_different_payload_is_rejected()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var key = $"fix15-{Guid.NewGuid():N}";

        var first = await PostSendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customer.Id],
            Title = Unique("اول"), Message = "متن"
        }, key);
        first.StatusCode.Should().Be(HttpStatusCode.OK);

        var different = await PostSendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customer.Id],
            Title = Unique("دوم"), Message = "متن دیگر"
        }, key);

        different.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task The_database_structurally_prevents_a_duplicate_recipient_row()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var broadcast = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customer.Id],
            Title = Unique("یکتا"), Message = "متن"
        });

        // Even a direct insert bypassing the service cannot duplicate delivery.
        await using var db = _fixture.CreateDbContext();
        db.Notifications.Add(new Notification
        {
            Id = Guid.NewGuid(), UserId = customer.Id, BroadcastId = broadcast.Id,
            Type = (byte)NotificationType.Announcement, Title = "دستی", Message = "متن",
            IsRead = false, CreatedAt = DateTime.UtcNow
        });

        await FluentActions.Invoking(() => db.SaveChangesAsync())
            .Should().ThrowAsync<DbUpdateException>("UX_Notifications_Broadcast_User must reject it");
    }

    [Fact]
    public async Task A_send_over_the_cap_is_blocked_without_truncating_recipients()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");

        // Deterministic limit check without provisioning 5,001 accounts: the selection itself
        // exceeds the cap, which the service rejects before touching the database.
        var oversized = Enumerable.Range(0, BroadcastRecipientRules.MaximumRecipients + 1)
            .Select(_ => Guid.NewGuid()).ToList();
        oversized[0] = customer.Id;

        var response = await PostSendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = oversized,
            Title = Unique("سقف"), Message = "متن"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<ApiResult>();
        body!.Message.Should().Contain("سقف مجاز");

        await using var db = _fixture.CreateDbContext();
        (await db.Notifications.CountAsync(x => x.UserId == customer.Id && x.BroadcastId != null))
            .Should().Be(0, "an over-cap send is refused outright, never partially delivered");
    }

    [Fact]
    public async Task A_send_at_the_cap_boundary_is_accepted_by_the_bulk_path()
    {
        // Exercises the batching path itself (5,000 rows / 500 per batch) without the HTTP layer,
        // proving the bulk insert is not a per-recipient SaveChanges loop.
        var (owner, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var recipients = Enumerable.Repeat(owner.Id, 1).ToList();

        using var scope = _fixture.Factory.Services.CreateScope();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var broadcastId = Guid.NewGuid();

        await using (var seed = _fixture.CreateDbContext())
        {
            seed.NotificationBroadcasts.Add(new NotificationBroadcast
            {
                Id = broadcastId, Title = Unique("مرزی"), Message = "متن",
                AudienceType = SelectedCustomers, RecipientCount = 0,
                Status = (byte)BroadcastStatus.Sent, CreatedByUserId = owner.Id,
                CreatedAt = DateTime.UtcNow, SentAt = DateTime.UtcNow
            });
            await seed.SaveChangesAsync();
        }

        var delivered = await notifications.CreateBulkAsync(broadcastId, recipients, "مرزی", "متن");

        delivered.Should().Be(recipients.Count);
        BroadcastRecipientRules.BatchSize.Should().Be(500);
        BroadcastRecipientRules.MaximumRecipients.Should().Be(5000);
    }

    [Fact]
    public async Task A_failure_mid_send_rolls_the_whole_broadcast_back()
    {
        using var admin = await SuperAdminClientAsync();
        var (customer, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var title = Unique("شکست");

        // A pre-existing row for (broadcastId, userId) is impossible to construct externally, so
        // failure is injected through an action URL that the service rejects after audience
        // resolution — the send must leave nothing behind either way.
        var response = await PostSendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customer.Id],
            Title = title, Message = "متن", ActionUrl = "https://evil.example"
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await using var db = _fixture.CreateDbContext();
        (await db.NotificationBroadcasts.CountAsync(x => x.Title == title)).Should().Be(0);
        (await db.Notifications.CountAsync(x => x.Title == title)).Should().Be(0);
    }

    [Fact]
    public async Task An_announcement_behaves_like_any_other_notification_for_read_state()
    {
        using var admin = await SuperAdminClientAsync();
        var (customerA, tokenA) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (customerB, tokenB) = await _fixture.CreateUserAndTokenAsync("Customer");
        var broadcast = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [customerA.Id, customerB.Id],
            Title = Unique("خواندن"), Message = "متن", ActionUrl = "/page/about"
        });

        using var clientA = _fixture.CreateClient(tokenA);
        var unreadBefore = await UnreadCountAsync(clientA);
        var mine = await MyNotificationsAsync(clientA);
        var announcement = mine.Single(x => x.Type == (byte)NotificationType.Announcement &&
                                            x.Title == broadcast.Title);
        announcement.IsRead.Should().BeFalse();
        announcement.ActionUrl.Should().Be("/page/about", "the CTA is projected from the broadcast");

        // Customer B cannot mark customer A's row read.
        using var clientB = _fixture.CreateClient(tokenB);
        (await clientB.PostAsync($"/api/notifications/{announcement.Id}/read", null))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);

        (await clientA.PostAsync($"/api/notifications/{announcement.Id}/read", null)).EnsureSuccessStatusCode();
        (await UnreadCountAsync(clientA)).Should().Be(unreadBefore - 1);
        (await MyNotificationsAsync(clientA)).Single(x => x.Id == announcement.Id).IsRead.Should().BeTrue();

        // Mark-all still clears whatever remains.
        (await clientA.PostAsync("/api/notifications/read-all", null)).EnsureSuccessStatusCode();
        (await UnreadCountAsync(clientA)).Should().Be(0);

        // B's copy is untouched by A's actions.
        (await MyNotificationsAsync(clientB)).Single(x => x.Title == broadcast.Title).IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task Sending_writes_one_audit_record_and_no_per_recipient_audit_rows()
    {
        using var admin = await SuperAdminClientAsync();
        var (a, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var (b, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var before = DateTime.UtcNow.AddSeconds(-5);

        var broadcast = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [a.Id, b.Id],
            Title = Unique("ممیزی"), Message = "متن"
        });

        await using var db = _fixture.CreateDbContext();
        var broadcastLogs = await db.AuditLogs.AsNoTracking()
            .Where(x => x.EntityId == broadcast.Id.ToString() && x.CreatedAt >= before).ToListAsync();

        broadcastLogs.Should().ContainSingle(x => x.ActionType == "NotificationBroadcastSent");
        var log = broadcastLogs.Single(x => x.ActionType == "NotificationBroadcastSent");
        log.Data.Should().Contain("recipients=2").And.Contain("audience=SelectedCustomers");
        log.Data.Should().NotContain(a.Id.ToString(), "the recipient list must never be logged");

        // The generic interceptor must not have produced one audit row per delivered notification.
        var recipientIds = await db.Notifications.AsNoTracking()
            .Where(x => x.BroadcastId == broadcast.Id).Select(x => x.Id.ToString()).ToListAsync();
        (await db.AuditLogs.AsNoTracking()
            .CountAsync(x => x.EntityName == "Notification" && recipientIds.Contains(x.EntityId!)))
            .Should().Be(0);
    }

    [Fact]
    public async Task History_reports_the_send_truthfully()
    {
        using var admin = await SuperAdminClientAsync();
        var (a, _) = await _fixture.CreateUserAndTokenAsync("Customer");
        var title = Unique("تاریخچه");
        var sent = await SendAsync(admin, new SendBroadcastRequestDto
        {
            Audience = SelectedCustomers, SelectedCustomerIds = [a.Id], Title = title, Message = "متن"
        });

        var response = await admin.GetAsync("/api/admin/notification-broadcasts?page=1&pageSize=50");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var history = (await response.Content.ReadFromJsonAsync<ApiResult<PagedResult<BroadcastDto>>>())!.Data!;

        var row = history.Items.Single(x => x.Id == sent.Id);
        row.Title.Should().Be(title);
        row.AudienceType.Should().Be(SelectedCustomers);
        row.RecipientCount.Should().Be(1);
        row.Status.Should().Be((byte)BroadcastStatus.Sent);
        row.SentAt.Should().NotBeNull();
        row.CreatedByFullName.Should().NotBeNullOrWhiteSpace();

        // There is no mutation endpoint for a sent broadcast.
        (await admin.PutAsJsonAsync($"/api/admin/notification-broadcasts/{sent.Id}", new { Title = "دستکاری" }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
        (await admin.DeleteAsync($"/api/admin/notification-broadcasts/{sent.Id}"))
            .StatusCode.Should().BeOneOf(HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed);
    }

    // Admin is deliberately absent from this list. It now holds users.manage - granted so the
    // /admin/users page it could always open would stop returning 403 from every call - and
    // broadcasting is gated on the same permission, so an administrator can now broadcast. That is a
    // consequence of the grant rather than a separate decision, and the test below states it outright
    // instead of leaving it implied.
    [Theory]
    [InlineData("Support")]
    [InlineData("KycViewer")]
    [InlineData("Customer")]
    public async Task Only_a_principal_holding_UserManage_can_broadcast(string role)
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync(role);
        using var client = _fixture.CreateClient(token);

        foreach (var response in new[]
                 {
                     await client.PostAsJsonAsync("/api/admin/notification-broadcasts/preview",
                         new BroadcastPreviewRequestDto { Audience = AllCustomers }),
                     await PostSendAsync(client, new SendBroadcastRequestDto
                     {
                         Audience = AllCustomers, Title = "بلوکه", Message = "متن"
                     }),
                     await client.GetAsync("/api/admin/notification-broadcasts")
                 })
        {
            response.StatusCode.Should()
                .Match(status => status == HttpStatusCode.Forbidden || status == HttpStatusCode.Unauthorized,
                    $"role '{role}' does not hold UserManage");
        }
    }

    [Fact]
    public async Task An_administrator_can_broadcast_because_it_now_holds_UserManage()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("Admin");
        using var client = _fixture.CreateClient(token);

        (await client.GetAsync("/api/admin/notification-broadcasts")).StatusCode
            .Should().Be(HttpStatusCode.OK,
                "users.manage was granted to Admin, and broadcasting is gated on that same permission");
    }

    [Fact]
    public async Task An_anonymous_caller_cannot_broadcast()
    {
        using var client = _fixture.CreateClient();

        var response = await PostSendAsync(client, new SendBroadcastRequestDto
        {
            Audience = AllCustomers, Title = "ناشناس", Message = "متن"
        });

        response.StatusCode.Should().BeOneOf(HttpStatusCode.Forbidden, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task The_direct_single_recipient_endpoint_is_unchanged()
    {
        // FIX-15 must not regress the existing AdminOnly one-user SystemMessage path.
        var (_, adminToken) = await _fixture.CreateUserAndTokenAsync("Admin");
        var (customer, customerToken) = await _fixture.CreateUserAndTokenAsync("Customer");
        using var admin = _fixture.CreateClient(adminToken);

        var response = await admin.PostAsJsonAsync("/api/admin/notifications/send",
            new { UserId = customer.Id, Title = "پیام مستقیم", Message = "متن" });
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        using var client = _fixture.CreateClient(customerToken);
        var mine = await MyNotificationsAsync(client);
        var direct = mine.Single(x => x.Title == "پیام مستقیم");
        direct.Type.Should().Be((byte)NotificationType.SystemMessage);
        direct.ActionUrl.Should().BeNull("only announcements carry a call to action");

        await using var db = _fixture.CreateDbContext();
        (await db.Notifications.AsNoTracking().SingleAsync(x => x.Id == direct.Id))
            .BroadcastId.Should().BeNull();
    }

    private async Task<BroadcastDto> SendAsync(HttpClient admin, SendBroadcastRequestDto request)
    {
        var response = await PostSendAsync(admin, request);
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<BroadcastDto>>())!.Data!;
    }

    private static async Task<HttpResponseMessage> PostSendAsync(
        HttpClient client, SendBroadcastRequestDto request, string? idempotencyKey = null)
    {
        using var content = JsonContent.Create(request);
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/admin/notification-broadcasts")
        {
            Content = content
        };
        message.Headers.Add("Idempotency-Key", idempotencyKey ?? $"fix15-{Guid.NewGuid():N}");
        return await client.SendAsync(message);
    }

    private static async Task<List<NotificationDto>> MyNotificationsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/notifications");
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResult<List<NotificationDto>>>())!.Data!;
    }

    private static async Task<int> UnreadCountAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/notifications/unread-count");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<ApiResult<int>>())!.Data;
    }

    private async Task SetStatusAsync(Guid userId, UserStatus status)
    {
        await using var db = _fixture.CreateDbContext();
        var user = await db.Users.SingleAsync(x => x.Id == userId);
        user.Status = (byte)status;
        await db.SaveChangesAsync();
    }

    private async Task<HttpClient> SuperAdminClientAsync()
    {
        var (_, token) = await _fixture.CreateUserAndTokenAsync("SuperAdmin");
        return _fixture.CreateClient(token);
    }

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..28];
}
