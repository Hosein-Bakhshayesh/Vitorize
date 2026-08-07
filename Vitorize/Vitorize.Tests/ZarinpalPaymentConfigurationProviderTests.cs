using Microsoft.Extensions.Hosting;
using NSubstitute;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Xunit;

namespace Vitorize.Tests;

public sealed class ZarinpalPaymentConfigurationProviderTests
{
    [Fact]
    public async Task GetAsync_reads_database_backed_settings_without_overlapping_operations()
    {
        var settings = Substitute.For<ISettingService>();
        var values = new Dictionary<string, string?>
        {
            [ZarinpalPaymentConfigurationProvider.MerchantIdKey] = "a5cfe6d8-340e-4c5d-90c8-804eb8e0fc2d",
            [ZarinpalPaymentConfigurationProvider.SandboxKey] = "true",
            [ZarinpalPaymentConfigurationProvider.BaseUrlKey] = "https://sandbox.zarinpal.com/pg/v4/payment/request.json",
            [ZarinpalPaymentConfigurationProvider.StartPayUrlKey] = "https://sandbox.zarinpal.com/pg/StartPay/",
            [ZarinpalPaymentConfigurationProvider.CallbackUrlKey] = "https://store.example/api/payments/zarinpal/callback"
        };
        var activeOperations = 0;

        settings.GetValueAsync(Arg.Any<string>()).Returns(async call =>
        {
            if (Interlocked.Increment(ref activeOperations) != 1)
                throw new InvalidOperationException("Settings reads overlapped on a scoped DbContext.");

            try
            {
                await Task.Yield();
                return values[call.Arg<string>()];
            }
            finally
            {
                Interlocked.Decrement(ref activeOperations);
            }
        });

        var environment = Substitute.For<IHostEnvironment>();
        environment.EnvironmentName.Returns("Testing");
        var provider = new ZarinpalPaymentConfigurationProvider(settings, environment);

        var result = await provider.GetAsync();

        Assert.Equal(values[ZarinpalPaymentConfigurationProvider.MerchantIdKey], result.MerchantId);
        await settings.Received(1).GetValueAsync(ZarinpalPaymentConfigurationProvider.CallbackUrlKey);
    }
}
