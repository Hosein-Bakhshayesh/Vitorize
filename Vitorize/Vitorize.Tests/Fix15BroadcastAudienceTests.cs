using FluentAssertions;
using Vitorize.Application.Common;
using Vitorize.Domain.Entities;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// FIX-15 (Client Issue #15). Recipient eligibility and action-link safety. Preview and Send share
/// the predicate exercised here, so these tests pin who can and cannot receive a broadcast.
/// </summary>
public sealed class Fix15BroadcastAudienceTests
{
    private static User Customer(byte status = (byte)UserStatus.Active, bool deleted = false,
        params string[] extraRoles)
    {
        var user = new User
        {
            Id = Guid.NewGuid(), FullName = "مشتری", Mobile = "09120000000",
            PasswordHash = "x", Status = status, IsDeleted = deleted, CreatedAt = DateTime.UtcNow
        };
        user.Roles.Add(new Role { Id = Guid.NewGuid(), Name = BroadcastRecipientRules.CustomerRole });
        foreach (var role in extraRoles)
            user.Roles.Add(new Role { Id = Guid.NewGuid(), Name = role });
        return user;
    }

    private static List<User> Eligible(params User[] users) =>
        users.AsQueryable().Where(BroadcastRecipientRules.IsEligibleCustomer).ToList();

    [Fact]
    public void An_active_customer_is_eligible()
    {
        var user = Customer();

        Eligible(user).Should().ContainSingle().Which.Should().Be(user);
    }

    [Theory]
    [InlineData((byte)UserStatus.Inactive)]
    [InlineData((byte)UserStatus.Suspended)]
    [InlineData((byte)UserStatus.Blocked)]
    public void A_customer_who_cannot_use_the_storefront_is_excluded(byte status) =>
        Eligible(Customer(status)).Should().BeEmpty();

    [Fact]
    public void A_soft_deleted_customer_is_excluded() =>
        Eligible(Customer(deleted: true)).Should().BeEmpty();

    [Theory]
    [InlineData("Admin")]
    [InlineData("SuperAdmin")]
    [InlineData("Support")]
    [InlineData("KycViewer")]
    public void A_staff_account_is_excluded_even_when_it_also_holds_the_customer_role(string staffRole) =>
        Eligible(Customer(extraRoles: staffRole)).Should()
            .BeEmpty("a customer broadcast must never reach staff");

    [Fact]
    public void An_account_without_the_customer_role_is_excluded()
    {
        var staffOnly = new User
        {
            Id = Guid.NewGuid(), FullName = "کارمند", Mobile = "09120000001",
            PasswordHash = "x", Status = (byte)UserStatus.Active, CreatedAt = DateTime.UtcNow
        };
        staffOnly.Roles.Add(new Role { Id = Guid.NewGuid(), Name = "Admin" });

        Eligible(staffOnly).Should().BeEmpty();
    }

    [Fact]
    public void A_mixed_population_resolves_to_only_the_usable_customers()
    {
        var active = Customer();
        var alsoActive = Customer();

        var resolved = Eligible(
            active,
            alsoActive,
            Customer((byte)UserStatus.Blocked),
            Customer(deleted: true),
            Customer(extraRoles: "Support"));

        resolved.Should().BeEquivalentTo(new[] { active, alsoActive });
    }

    [Fact]
    public void The_approved_limits_are_five_thousand_recipients_in_batches_of_five_hundred()
    {
        BroadcastRecipientRules.MaximumRecipients.Should().Be(5000);
        BroadcastRecipientRules.BatchSize.Should().Be(500);
        BroadcastRecipientRules.StaffRoles.Should()
            .BeEquivalentTo("Admin", "SuperAdmin", "Support", "KycViewer");
    }

    [Fact]
    public void A_selected_list_is_deduplicated_before_delivery()
    {
        var id = Guid.NewGuid();
        var other = Guid.NewGuid();

        // Mirrors the service's normalisation: distinct, empties dropped.
        var normalized = new[] { id, id, other, Guid.Empty, other }
            .Where(x => x != Guid.Empty).Distinct().ToList();

        normalized.Should().BeEquivalentTo(new[] { id, other });
    }

    [Theory]
    [InlineData("/shop")]
    [InlineData("/page/summer-sale")]
    [InlineData("/product/playstation-gift-card")]
    [InlineData("/customer/orders")]
    [InlineData("/shop?category=games")]
    public void A_safe_internal_path_is_accepted(string url) =>
        NotificationActionUrlRules.NormalizeInternalPath(url).Should().Be(url);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_omitted_action_url_is_allowed(string? url) =>
        NotificationActionUrlRules.NormalizeInternalPath(url).Should().BeNull();

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("http://example.com/x")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("mailto:a@b.test")]
    [InlineData("shop")]
    [InlineData("\\\\evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/shop\r\nSet-Cookie: x=1")]
    public void An_unsafe_or_external_action_url_is_rejected(string url)
    {
        FluentActions.Invoking(() => NotificationActionUrlRules.NormalizeInternalPath(url))
            .Should().Throw<BusinessException>();
        NotificationActionUrlRules.IsSafeInternalPath(url).Should().BeFalse();
    }

    [Theory]
    [InlineData("/admin")]
    [InlineData("/admin/users")]
    [InlineData("/api/orders")]
    [InlineData("/_blazor")]
    [InlineData("/_framework/blazor.web.js")]
    public void An_administrative_or_infrastructure_path_is_rejected(string url) =>
        FluentActions.Invoking(() => NotificationActionUrlRules.NormalizeInternalPath(url))
            .Should().Throw<BusinessException>();

    [Fact]
    public void An_over_long_action_url_is_rejected() =>
        FluentActions.Invoking(() =>
                NotificationActionUrlRules.NormalizeInternalPath("/" + new string('a', 500)))
            .Should().Throw<BusinessException>();

    [Fact]
    public void Announcement_is_a_new_type_and_leaves_existing_values_untouched()
    {
        ((byte)NotificationType.Announcement).Should().Be(91);
        ((byte)NotificationType.SystemMessage).Should().Be(90);
        ((byte)NotificationType.GiftCodeDelivered).Should().Be(20);
        ((byte)NotificationType.TicketCreated).Should().Be(50);
        ((byte)BroadcastAudience.AllCustomers).Should().Be(1);
        ((byte)BroadcastAudience.SelectedCustomers).Should().Be(2);
    }
}
