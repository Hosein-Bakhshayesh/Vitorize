using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Helpers;
using Vitorize.Infrastructure.Services;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Exceptions;
using Vitorize.Shared.Logging;
using Xunit;

namespace Vitorize.Tests.Unit;

[Trait("Category", "Unit")]
public sealed class SmsAndSecurityUnitTests
{
    private const string EncryptionKey = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData(SmsTemplateKeys.LoginOtp)]
    [InlineData(SmsTemplateKeys.RegisterOtp)]
    [InlineData(SmsTemplateKeys.ForgotPassword)]
    [InlineData(SmsTemplateKeys.GenericOtp)]
    public void Every_otp_template_has_exact_code_expire_contract(string template)
    {
        SmsTemplateContract.GetRequiredParameterNames(template).Should().Equal("CODE", "EXPIRE");
        SmsTemplateContract.HasExactParameters(template,
        [
            new("CODE", "483921"),
            new("EXPIRE", "3")
        ]).Should().BeTrue();
    }

    [Theory]
    [InlineData(SmsTemplateKeys.OrderPaid)]
    [InlineData(SmsTemplateKeys.GiftCodeDelivered)]
    [InlineData(SmsTemplateKeys.TicketReply)]
    [InlineData(SmsTemplateKeys.WalletTopUpSuccess)]
    [InlineData(SmsTemplateKeys.VerificationApproved)]
    [InlineData(SmsTemplateKeys.VerificationRejected)]
    public void Every_business_notification_has_only_order_number(string template)
    {
        SmsTemplateContract.GetRequiredParameterNames(template).Should().Equal("ORDER_NUMBER");
        SmsTemplateContract.HasExactParameters(template, [new("ORDER_NUMBER", "VT-123456")]).Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(InvalidNotificationParameters))]
    public void Notification_contract_rejects_missing_duplicate_legacy_or_unknown_parameters(
        IReadOnlyList<SmsTemplateParameter> parameters)
    {
        SmsTemplateContract.HasExactParameters(SmsTemplateKeys.OrderPaid, parameters).Should().BeFalse();
    }

    public static TheoryData<IReadOnlyList<SmsTemplateParameter>> InvalidNotificationParameters => new()
    {
        Array.Empty<SmsTemplateParameter>(),
        new[] { new SmsTemplateParameter("ORDER_NUMBER", "") },
        new[] { new SmsTemplateParameter("REFERENCE", "VT-1") },
        new[] { new SmsTemplateParameter("DETAIL", "secret") },
        new[] { new SmsTemplateParameter("ORDER_NUMBER", "VT-1"), new SmsTemplateParameter("ORDER_NUMBER", "VT-1") },
        new[] { new SmsTemplateParameter("ORDER_NUMBER", "VT-1"), new SmsTemplateParameter("TITLE", "unsafe") }
    };

    [Fact]
    public void Legacy_outbox_reference_maps_to_order_number_and_drops_title_detail()
    {
        var normalized = SmsTemplateContract.NormalizeQueuedParameters(SmsTemplateKeys.OrderPaid,
        [
            new("TITLE", "old title"),
            new("REFERENCE", "VT-100254"),
            new("DETAIL", "amount 1000")
        ]);

        normalized.Should().ContainSingle().Which.Should().Be(new SmsTemplateParameter("ORDER_NUMBER", "VT-100254"));
    }

    [Fact]
    public void Existing_order_number_wins_over_legacy_reference()
    {
        var normalized = SmsTemplateContract.NormalizeQueuedParameters(SmsTemplateKeys.OrderPaid,
        [
            new("ORDER_NUMBER", "VT-NEW"),
            new("REFERENCE", "VT-OLD")
        ]);

        normalized.Should().ContainSingle().Which.Value.Should().Be("VT-NEW");
    }

    [Fact]
    public void Public_references_are_stable_prefixed_and_never_expose_guid()
    {
        var id = Guid.Parse("44444444-4444-4444-4444-444444444444");

        var ticket = SmsPublicReference.ForTicket(id);
        var wallet = SmsPublicReference.ForWallet(id);
        var verification = SmsPublicReference.ForVerification(id);

        ticket.Should().MatchRegex("^TK-[0-9A-F]{12}$");
        wallet.Should().MatchRegex("^WL-[0-9A-F]{12}$");
        verification.Should().MatchRegex("^VF-[0-9A-F]{12}$");
        ticket.Should().Be(SmsPublicReference.ForTicket(id));
        new[] { ticket, wallet, verification }.Should().OnlyContain(x => !x.Contains(id.ToString("N"), StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(SmsFailureReason.Network, true)]
    [InlineData(SmsFailureReason.Timeout, true)]
    [InlineData(SmsFailureReason.ProviderUnavailable, true)]
    [InlineData(SmsFailureReason.TooManyRequests, true)]
    [InlineData(SmsFailureReason.Unauthorized, false)]
    [InlineData(SmsFailureReason.InvalidParameter, false)]
    public void Retry_policy_distinguishes_transient_from_permanent_failures(SmsFailureReason reason, bool expected)
    {
        SmsRetryPolicy.IsRetryable(reason).Should().Be(expected);
    }

    [Fact]
    public async Task Sms_service_uses_normalized_mobile_shared_template_and_exact_otp_parameters()
    {
        var settings = Substitute.For<ISmsSettingsProvider>();
        var sender = Substitute.For<ISmsSender>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(UnitFixtures.SmsOptions());
        sender.SendVerifyAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(),
                Arg.Any<IReadOnlyList<SmsTemplateParameter>>(), Arg.Any<CancellationToken>())
            .Returns(SmsSendResult.Success("message-1"));
        var sut = new SmsService(settings, sender, Substitute.For<ILogger<SmsService>>());

        var result = await sut.SendLoginOtpAsync("+98 912 345 6789", "483921", 3);

        result.IsSuccess.Should().BeTrue();
        await sender.Received(1).SendVerifyAsync("unit-test-api-key", "09123456789", 101,
            Arg.Is<IReadOnlyList<SmsTemplateParameter>>(x =>
                x.Count == 2 && x[0].Name == "CODE" && x[0].Value == "483921" &&
                x[1].Name == "EXPIRE" && x[1].Value == "3"), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, true, SmsFailureReason.Disabled)]
    [InlineData(true, false, SmsFailureReason.NotConfigured)]
    public async Task Sms_service_fails_before_provider_when_disabled_or_missing_key(
        bool enabled, bool hasKey, SmsFailureReason expected)
    {
        var options = new SmsOptions { IsEnabled = enabled, ApiKey = hasKey ? "key" : null };
        var settings = Substitute.For<ISmsSettingsProvider>();
        var sender = Substitute.For<ISmsSender>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(options);
        var sut = new SmsService(settings, sender, Substitute.For<ILogger<SmsService>>());

        var result = await sut.SendTemplateAsync("09123456789", SmsTemplateKeys.LoginOtp,
            [new("CODE", "123456"), new("EXPIRE", "3")]);

        result.FailureReason.Should().Be(expected);
        await sender.DidNotReceiveWithAnyArgs().SendVerifyAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public async Task Sms_service_rejects_unknown_or_legacy_parameters_without_provider_call()
    {
        var settings = Substitute.For<ISmsSettingsProvider>();
        var sender = Substitute.For<ISmsSender>();
        settings.GetAsync(Arg.Any<CancellationToken>()).Returns(UnitFixtures.SmsOptions());
        var sut = new SmsService(settings, sender, Substitute.For<ILogger<SmsService>>());

        var result = await sut.SendTemplateAsync("09123456789", SmsTemplateKeys.OrderPaid, [new("REFERENCE", "VT-1")]);

        result.FailureReason.Should().Be(SmsFailureReason.InvalidParameter);
        await sender.DidNotReceiveWithAnyArgs().SendVerifyAsync(default!, default!, default, default!, default);
    }

    [Fact]
    public void Encryption_is_nondeterministic_authenticated_and_rejects_bad_configuration()
    {
        var sut = new AesEncryptionService(Options.Create(new EncryptionSettings { Key = EncryptionKey }));
        var first = sut.Encrypt("gift-code-value");
        var second = sut.Encrypt("gift-code-value");

        first.Should().StartWith("v2.").And.NotBe(second);
        sut.Decrypt(first).Should().Be("gift-code-value");
        ((Func<string>)(() => sut.Encrypt(""))).Should().Throw<BusinessException>();
        ((Func<string>)(() => new AesEncryptionService(Options.Create(new EncryptionSettings { Key = "short" })).Encrypt("x")))
            .Should().Throw<BusinessException>();
        ((Func<string>)(() => sut.Decrypt("v2.invalid"))).Should().Throw<BusinessException>();
    }

    [Fact]
    public void Request_fingerprint_is_deterministic_value_sensitive_and_supports_null()
    {
        RequestHashHelper.ComputeHash(new { Id = 1, Value = "x" })
            .Should().Be(RequestHashHelper.ComputeHash(new { Id = 1, Value = "x" }));
        RequestHashHelper.ComputeHash(new { Id = 1, Value = "x" })
            .Should().NotBe(RequestHashHelper.ComputeHash(new { Id = 2, Value = "x" }));
        RequestHashHelper.ComputeHash(null).Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Html_sanitizer_returns_null_for_empty_input(string? html)
    {
        new StrictHtmlContentSanitizer().Sanitize(html).Should().BeNull();
    }

    [Fact]
    public void Html_sanitizer_rejects_oversized_content_and_removes_unsafe_css_and_data_urls()
    {
        var sut = new StrictHtmlContentSanitizer();
        ((Func<string?>)(() => sut.Sanitize(new string('x', 200_001)))).Should().Throw<BusinessException>();

        var safe = sut.Sanitize("<div style=\"background:url(javascript:alert(1))\" onclick=\"x()\"><img src=\"data:text/html;base64,WA==\"></div>");
        safe!.ToLowerInvariant().Should().NotContain("style")
            .And.NotContain("onclick")
            .And.NotContain("data:text")
            .And.NotContain("javascript");
    }

    [Fact]
    public void Sensitive_log_redaction_bounds_and_masks_free_text()
    {
        var value = "Bearer secret-token admin@example.com 09123456789 password=NeverLog\r\n" + new string('x', 500);
        var safe = SensitiveLogData.RedactFreeText(value, 100);

        safe.Length.Should().BeLessOrEqualTo(100);
        safe.Should().NotContain("secret-token").And.NotContain("NeverLog")
            .And.NotContain("admin@example.com").And.NotContain("09123456789")
            .And.NotContain("\r").And.NotContain("\n");
    }
}
