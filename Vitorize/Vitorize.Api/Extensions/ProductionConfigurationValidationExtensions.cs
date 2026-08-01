using Vitorize.Infrastructure.Common.Zarinpal;

namespace Vitorize.Api.Extensions;

public static class ProductionConfigurationValidationExtensions
{
    public static void ValidateProductionPaymentConfiguration(this WebApplication app)
    {
        if (!app.Environment.IsProduction()) return;

        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IZarinpalPaymentConfigurationProvider>();
        var configuration = provider.GetAsync().GetAwaiter().GetResult();
        if (ZarinpalPaymentConfigurationRules.IsDeploymentPlaceholder(configuration.MerchantId))
        {
            // A fresh database needs to boot so the one-time SuperAdmin can configure
            // Payment settings. The gateway separately rejects this sentinel before it
            // can issue an external request or treat an order as paid.
            app.Logger.LogWarning("Production is running with the Zarinpal deployment placeholder; gateway payments remain unavailable until Payment settings are configured. EventType={EventType}", "ZarinpalDeploymentPlaceholder");
            return;
        }

        var validation = provider.ValidateAsync().GetAwaiter().GetResult();
        if (!validation.IsValid)
        {
            // Messages intentionally describe missing or inconsistent keys, never their values.
            throw new InvalidOperationException(
                "Production payment configuration is invalid: " + string.Join(" ", validation.Errors));
        }
    }
}
