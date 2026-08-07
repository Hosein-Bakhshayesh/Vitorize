using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NSubstitute;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Common.Zarinpal;
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
        var configuration = Substitute.For<IZarinpalPaymentConfigurationProvider>();
        var gateway = new ZarinpalGatewayService(
            new HttpClient(), configuration, Env("Testing"),
            Faults(new TestingFaultInjectionOptions { Payment = "VerifyFail" }));

        var (success, refId) = await gateway.VerifyPaymentAsync("authority", 100m);

        Assert.False(success);
        Assert.Equal(0, refId);
        // The fault short-circuits before any gateway configuration call.
        await configuration.DidNotReceive().GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Payment_verify_fault_is_ignored_outside_testing_environment()
    {
        var configuration = Substitute.For<IZarinpalPaymentConfigurationProvider>();
        configuration.GetAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalPaymentConfiguration("", null, null, null, null));
        configuration.ValidateAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalConfigurationValidation(false, new[] { "invalid" }));
        var gateway = new ZarinpalGatewayService(
            new HttpClient(), configuration, Env("Production"),
            Faults(new TestingFaultInjectionOptions { Payment = "VerifyFail" }));

        // Fault ignored in Production -> real configuration validation runs and fails safely.
        var result = await gateway.VerifyPaymentAsync("authority", 100m);
        Assert.False(result.Success);
        await configuration.Received(1).GetAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Production_deployment_placeholder_never_invokes_gateway()
    {
        var configuration = Substitute.For<IZarinpalPaymentConfigurationProvider>();
        configuration.GetAsync(Arg.Any<CancellationToken>()).Returns(new ZarinpalPaymentConfiguration(
            Guid.Empty.ToString(), false,
            new Uri("https://payment.zarinpal.com/pg/v4/payment"),
            new Uri("https://payment.zarinpal.com/pg/StartPay"),
            new Uri("https://vitorize.invalid/api/payments/zarinpal/callback")));
        var gateway = new ZarinpalGatewayService(
            new HttpClient(), configuration, Env("Production"), Faults(new TestingFaultInjectionOptions()));

        var result = await gateway.CreatePaymentAsync(100m, CurrencyType.Toman, "certification");

        Assert.False(result.Success);
        await configuration.DidNotReceive().ValidateAsync(Arg.Any<CancellationToken>());
    }
}
