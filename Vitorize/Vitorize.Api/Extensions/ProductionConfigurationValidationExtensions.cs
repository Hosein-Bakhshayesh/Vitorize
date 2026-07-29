using Vitorize.Infrastructure.Common.Zarinpal;

namespace Vitorize.Api.Extensions;

public static class ProductionConfigurationValidationExtensions
{
    public static void ValidateProductionPaymentConfiguration(this WebApplication app)
    {
        if (!app.Environment.IsProduction()) return;

        using var scope = app.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IZarinpalPaymentConfigurationProvider>();
        var validation = provider.ValidateAsync().GetAwaiter().GetResult();
        if (!validation.IsValid)
        {
            // Messages intentionally describe missing or inconsistent keys, never their values.
            throw new InvalidOperationException(
                "Production payment configuration is invalid: " + string.Join(" ", validation.Errors));
        }
    }
}
