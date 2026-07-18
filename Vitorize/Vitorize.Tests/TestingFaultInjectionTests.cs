using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Services;
using Vitorize.Infrastructure.Services.Sms;
using Vitorize.Infrastructure.Services.Testing;
using Vitorize.Shared.Enums;
using Xunit;

namespace Vitorize.Tests;

/// <summary>
/// Phase 4 fault-injection guard tests. The Testing-only fault injection must (a) produce the
/// configured failure when the host is in the Testing environment, and (b) be completely inert in
/// any other environment - so it can never weaken Production or Development behaviour.
/// </summary>
public sealed class TestingFaultInjectionTests
{
    private static IHostEnvironment Env(string name)
    {
        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName = name;
        return environment;
    }

    private static IOptionsMonitor<TestingFaultInjectionOptions> Faults(TestingFaultInjectionOptions options)
    {
        var monitor = Substitute.For<IOptionsMonitor<TestingFaultInjectionOptions>>();
        monitor.CurrentValue.Returns(options);
        return monitor;
    }

    [Theory]
    [InlineData("Network", SmsFailureReason.Network)]
    [InlineData("Timeout", SmsFailureReason.Timeout)]
    [InlineData("Unavailable", SmsFailureReason.ProviderUnavailable)]
    [InlineData("Fail", SmsFailureReason.Unknown)]
    public async Task Sms_fault_is_injected_in_testing_environment(string mode, SmsFailureReason expected)
    {
        var sender = new TestingSmsSender(Faults(new TestingFaultInjectionOptions { Sms = mode }), Env("Testing"));

        var result = await sender.SendVerifyAsync("key", "09120000000", 1, Array.Empty<SmsTemplateParameter>());

        Assert.False(result.IsSuccess);
        Assert.Equal(expected, result.FailureReason);
    }

    [Fact]
    public async Task Sms_fault_is_ignored_outside_testing_environment()
    {
        // Same fault configuration, but Production must never honour it.
        var sender = new TestingSmsSender(Faults(new TestingFaultInjectionOptions { Sms = "Timeout" }), Env("Production"));

        var result = await sender.SendVerifyAsync("key", "09120000000", 1, Array.Empty<SmsTemplateParameter>());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Sms_default_off_returns_success_even_in_testing()
    {
        var sender = new TestingSmsSender(Faults(new TestingFaultInjectionOptions()), Env("Testing"));

        var result = await sender.SendVerifyAsync("key", "09120000000", 1, Array.Empty<SmsTemplateParameter>());

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Payment_verify_fault_is_injected_in_testing_environment()
    {
        var settings = Substitute.For<ISettingService>();
        var gateway = new ZarinpalGatewayService(
            new HttpClient(), settings, Env("Testing"),
            Faults(new TestingFaultInjectionOptions { Payment = "VerifyFail" }));

        var (success, refId) = await gateway.VerifyPaymentAsync("authority", 100m);

        Assert.False(success);
        Assert.Equal(0, refId);
        // The fault short-circuits before any gateway/setting call.
        await settings.DidNotReceive().GetValueAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task Payment_verify_fault_is_ignored_outside_testing_environment()
    {
        var settings = Substitute.For<ISettingService>();
        settings.GetValueAsync("ZarinpalMerchantId").Returns((string?)null);
        var gateway = new ZarinpalGatewayService(
            new HttpClient(), settings, Env("Production"),
            Faults(new TestingFaultInjectionOptions { Payment = "VerifyFail" }));

        // Fault ignored in Production -> the real path runs and fails on the missing merchant setting,
        // proving the injected VerifyFail was NOT applied.
        await Assert.ThrowsAsync<InvalidOperationException>(() => gateway.VerifyPaymentAsync("authority", 100m));
    }
}
