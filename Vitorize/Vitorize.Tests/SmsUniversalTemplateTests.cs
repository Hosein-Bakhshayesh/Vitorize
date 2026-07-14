using Microsoft.Extensions.Logging.Abstractions;
using Vitorize.Application.Common;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services.Sms;
using Xunit;

namespace Vitorize.Tests;

public class SmsUniversalTemplateTests
{
    [Fact]
    public void BuildTemplateIds_MapsEveryFlowToExactlyTwoCanonicalIds()
    {
        var settings = new Dictionary<string, string?>
        {
            [SmsSettingKeys.OtpTemplateId] = "101",
            [SmsSettingKeys.LoginOtpTemplateId] = "999",
            [SmsSettingKeys.NotificationTemplateId] = "202",
            [SmsSettingKeys.OrderPaidTemplateId] = "888"
        };

        var result = SmsSettingsProvider.BuildTemplateIds(settings);

        Assert.All(SmsTemplateKeys.OtpTemplates, key => Assert.Equal(101, result[key]));
        Assert.All(SmsTemplateKeys.NotificationTemplates, key => Assert.Equal(202, result[key]));
        Assert.Equal(2, result.Values.Distinct().Count());
    }

    [Fact]
    public void BuildTemplateIds_FallsBackToLegacyKeysForExistingInstallations()
    {
        var settings = new Dictionary<string, string?>
        {
            [SmsSettingKeys.RegisterOtpTemplateId] = "303",
            [SmsSettingKeys.TicketReplyTemplateId] = "404"
        };

        var result = SmsSettingsProvider.BuildTemplateIds(settings);

        Assert.All(SmsTemplateKeys.OtpTemplates, key => Assert.Equal(303, result[key]));
        Assert.All(SmsTemplateKeys.NotificationTemplates, key => Assert.Equal(404, result[key]));
    }

    [Fact]
    public void SettingGroups_ContainPrimaryAndEveryLegacyKey()
    {
        Assert.Equal(SmsSettingKeys.OtpTemplateId, SmsSettingKeys.OtpTemplateIdKeys[0]);
        Assert.Equal(4, SmsSettingKeys.OtpTemplateIdKeys.Count);
        Assert.Equal(SmsSettingKeys.NotificationTemplateId, SmsSettingKeys.NotificationTemplateIdKeys[0]);
        Assert.Equal(9, SmsSettingKeys.NotificationTemplateIdKeys.Count);

        Assert.True(SmsSettingKeys.TryGetTemplateIdGroup(
            SmsSettingKeys.ForgotPasswordTemplateId, out var otpGroup));
        Assert.Same(SmsSettingKeys.OtpTemplateIdKeys, otpGroup);

        Assert.True(SmsSettingKeys.TryGetTemplateIdGroup(
            SmsSettingKeys.WalletTopUpSuccessTemplateId, out var notificationGroup));
        Assert.Same(SmsSettingKeys.NotificationTemplateIdKeys, notificationGroup);
    }

    [Fact]
    public void NotificationContract_AcceptsOnlyOrderNumber()
    {
        Assert.True(IsValidNotification(
            new SmsTemplateParameter(SmsTemplateParams.OrderNumber, "VT-123456")));

        Assert.False(IsValidNotification(new SmsTemplateParameter("TITLE", "عنوان")));
        Assert.False(IsValidNotification(new SmsTemplateParameter("REFERENCE", "VT-123456")));
        Assert.False(IsValidNotification(new SmsTemplateParameter("DETAIL", "جزئیات")));
        Assert.False(IsValidNotification());
        Assert.False(IsValidNotification(
            new SmsTemplateParameter(SmsTemplateParams.OrderNumber, "VT-1"),
            new SmsTemplateParameter(SmsTemplateParams.OrderNumber, "VT-2")));
        Assert.False(IsValidNotification(
            new SmsTemplateParameter(SmsTemplateParams.OrderNumber, "VT-123456"),
            new SmsTemplateParameter("UNKNOWN", "value")));
        Assert.False(IsValidNotification(new SmsTemplateParameter(SmsTemplateParams.OrderNumber, "")));
    }

    [Fact]
    public void QueuedParameters_MapsLegacyReferenceAndIgnoresLegacyDetail()
    {
        var normalized = SmsTemplateContract.NormalizeQueuedParameters(
            SmsTemplateKeys.OrderPaid,
        [
            new("TITLE", "پرداخت سفارش با موفقیت انجام شد"),
            new("REFERENCE", "سفارش VT-123456"),
            new("DETAIL", "مبلغ قدیمی")
        ]);

        Assert.True(SmsTemplateContract.HasExactParameters(SmsTemplateKeys.OrderPaid, normalized));
        Assert.Equal(new[] { "ORDER_NUMBER" }, normalized.Select(x => x.Name));
        Assert.Equal("VT-123456", normalized[0].Value);
        Assert.DoesNotContain(normalized, x => x.Name is "TITLE" or "REFERENCE" or "DETAIL");
    }

    [Fact]
    public void QueuedParameters_RemovesLegacyTitleAndDetailWhenOrderNumberAlreadyExists()
    {
        var normalized = SmsTemplateContract.NormalizeQueuedParameters(
            SmsTemplateKeys.OrderCompleted,
        [
            new("TITLE", "سفارش شما تکمیل شد"),
            new(SmsTemplateParams.OrderNumber, "VT-654321"),
            new("DETAIL", "جزئیات قدیمی")
        ]);

        var parameter = Assert.Single(normalized);
        Assert.Equal(SmsTemplateParams.OrderNumber, parameter.Name);
        Assert.Equal("VT-654321", parameter.Value);
        Assert.True(SmsTemplateContract.HasExactParameters(
            SmsTemplateKeys.OrderCompleted, normalized));
    }

    [Fact]
    public void QueuedParameters_PrefersCurrentReferenceAndStillRejectsDuplicateOrUnknownCurrentParameters()
    {
        var currentAndLegacyReference = SmsTemplateContract.NormalizeQueuedParameters(
            SmsTemplateKeys.OrderPaid,
        [
            new("TITLE", "عنوان"),
            new(SmsTemplateParams.OrderNumber, "VT-1"),
            new("REFERENCE", "VT-2")
        ]);

        var duplicateCurrentReference = SmsTemplateContract.NormalizeQueuedParameters(
            SmsTemplateKeys.OrderPaid,
        [
            new(SmsTemplateParams.OrderNumber, "VT-1"),
            new(SmsTemplateParams.OrderNumber, "VT-2")
        ]);

        var unknown = SmsTemplateContract.NormalizeQueuedParameters(
            SmsTemplateKeys.OrderPaid,
        [
            new("REFERENCE", "VT-1"),
            new("UNKNOWN", "value")
        ]);

        Assert.True(SmsTemplateContract.HasExactParameters(
            SmsTemplateKeys.OrderPaid, currentAndLegacyReference));
        Assert.Equal("VT-1", currentAndLegacyReference.Single().Value);
        Assert.False(SmsTemplateContract.HasExactParameters(
            SmsTemplateKeys.OrderPaid, duplicateCurrentReference));
        Assert.False(SmsTemplateContract.HasExactParameters(
            SmsTemplateKeys.OrderPaid, unknown));
    }

    [Theory]
    [InlineData("WalletTopUpSuccess", "مبلغ 100,000 تومان", "WL-")]
    [InlineData("VerificationApproved", "کاربر علی", "VF-")]
    [InlineData("TicketReply", "تیکت A1B2C3D4", "TK-")]
    public void QueuedParameters_LegacySensitiveReference_IsReplacedByOpaquePublicReference(
        string templateKey,
        string legacyReference,
        string expectedPrefix)
    {
        var normalized = SmsTemplateContract.NormalizeQueuedParameters(templateKey,
        [
            new("TITLE", "عنوان"),
            new("REFERENCE", legacyReference),
            new("DETAIL", "جزئیات قدیمی")
        ]);

        var publicReference = normalized.Single(x => x.Name == SmsTemplateParams.OrderNumber).Value;
        Assert.StartsWith(expectedPrefix, publicReference);
        Assert.DoesNotContain(legacyReference, publicReference, StringComparison.Ordinal);
        Assert.True(SmsTemplateContract.HasExactParameters(templateKey, normalized));
    }

    [Fact]
    public void AllowedBusinessMappings_ContainOnlyApprovedPublicReference()
    {
        AssertParameters(SmsBusinessNotificationParameters.OrderPaid("VT-2"), "VT-2");
        AssertParameters(SmsBusinessNotificationParameters.GiftCodeDelivered("VT-5"), "VT-5");
        AssertParameters(SmsBusinessNotificationParameters.TicketReply("TK-1"), "TK-1");
        AssertParameters(SmsBusinessNotificationParameters.WalletTopUp("WL-1"), "WL-1");
        AssertParameters(SmsBusinessNotificationParameters.VerificationApproved("VF-1"), "VF-1");
        AssertParameters(SmsBusinessNotificationParameters.VerificationRejected("VF-2"), "VF-2");
    }

    [Fact]
    public void AutomaticEventPolicy_AllowsOnlyTheApprovedFlows()
    {
        Assert.Equal(
            new[] { "ForgotPassword", "LoginOtp", "RegisterOtp" },
            SmsAutomaticEventPolicy.AllowedOtpTemplates.OrderBy(x => x));
        Assert.Equal(
            new[]
            {
                "GiftCodeDelivered", "OrderPaid", "TicketReply",
                "VerificationApproved", "VerificationRejected", "WalletTopUpSuccess"
            },
            SmsAutomaticEventPolicy.AllowedNotificationTemplates.OrderBy(x => x));

        Assert.All(SmsAutomaticEventPolicy.RemovedAutomaticTemplates,
            key => Assert.False(SmsAutomaticEventPolicy.IsAllowedTemplate(key), key));
        Assert.DoesNotContain(SmsTemplateKeys.GenericOtp,
            SmsAutomaticEventPolicy.AllowedOtpTemplates);
    }

    [Fact]
    public void RetryPolicy_RetriesOnlyTransientFailures()
    {
        var retryable = Enum.GetValues<Vitorize.Shared.Enums.SmsFailureReason>()
            .Where(SmsRetryPolicy.IsRetryable)
            .OrderBy(x => x)
            .ToArray();

        Assert.Equal(new[]
        {
            Vitorize.Shared.Enums.SmsFailureReason.TooManyRequests,
            Vitorize.Shared.Enums.SmsFailureReason.Timeout,
            Vitorize.Shared.Enums.SmsFailureReason.Network,
            Vitorize.Shared.Enums.SmsFailureReason.ProviderUnavailable
        }, retryable);
    }

    [Fact]
    public void AdminMobileMask_HidesMiddleDigits()
    {
        Assert.Equal("0912***6789", IranMobile.Mask("09123456789"));
    }

    [Fact]
    public void PublicReferences_AreStablePrefixedAndNeverExposeGuid()
    {
        var id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var raw = id.ToString("N");

        var ticket = SmsPublicReference.ForTicket(id);
        var wallet = SmsPublicReference.ForWallet(id);
        var verification = SmsPublicReference.ForVerification(id);

        Assert.Matches("^TK-[A-F0-9]{12}$", ticket);
        Assert.Matches("^WL-[A-F0-9]{12}$", wallet);
        Assert.Matches("^VF-[A-F0-9]{12}$", verification);
        Assert.Equal(ticket, SmsPublicReference.ForTicket(id));
        Assert.DoesNotContain(raw, ticket, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(raw, wallet, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(raw, verification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EveryLogicalSmsFlow_SendsThroughItsUniversalTemplate()
    {
        var templateIds = SmsSettingsProvider.BuildTemplateIds(new Dictionary<string, string?>
        {
            [SmsSettingKeys.OtpTemplateId] = "101",
            [SmsSettingKeys.NotificationTemplateId] = "202"
        });
        var options = new SmsOptions
        {
            IsEnabled = true,
            ApiKey = "test-key",
            TemplateIds = templateIds,
            MaxRetryCount = 0
        };

        foreach (var templateKey in SmsTemplateKeys.OtpTemplates)
        {
            var sender = new FakeSmsSender();
            var service = Build(options, sender);
            var result = await service.SendTemplateAsync("09123456789", templateKey,
            [
                new(SmsTemplateParams.Code, "123456"),
                new(SmsTemplateParams.Expire, "3")
            ]);

            Assert.True(result.IsSuccess, templateKey);
            Assert.Equal(101, sender.LastTemplateId);
        }

        foreach (var templateKey in SmsTemplateKeys.NotificationTemplates)
        {
            var sender = new FakeSmsSender();
            var service = Build(options, sender);
            var result = await service.SendTemplateAsync(
                "09123456789",
                templateKey,
                SmsBusinessNotificationParameters.Create("VT-123456"));

            Assert.True(result.IsSuccess, templateKey);
            Assert.Equal(202, sender.LastTemplateId);
        }
    }

    private static SmsService Build(SmsOptions options, FakeSmsSender sender) =>
        new(new FakeSmsSettingsProvider(options), sender, NullLogger<SmsService>.Instance);

    private static bool IsValidNotification(params SmsTemplateParameter[] parameters) =>
        SmsTemplateContract.HasExactParameters(SmsTemplateKeys.UniversalNotification, parameters);

    private static void AssertParameters(
        IReadOnlyList<SmsTemplateParameter> parameters,
        string orderNumber)
    {
        var parameter = Assert.Single(parameters);
        Assert.Equal((SmsTemplateParams.OrderNumber, orderNumber), (parameter.Name, parameter.Value));
    }
}
