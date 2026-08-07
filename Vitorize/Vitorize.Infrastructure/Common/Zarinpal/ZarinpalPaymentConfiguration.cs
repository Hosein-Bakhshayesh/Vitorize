using Microsoft.Extensions.Hosting;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Common.Zarinpal;

/// <summary>
/// The single authoritative gateway configuration. Values are intentionally loaded from the
/// protected Payment settings group, including the callback URL.
/// </summary>
public sealed record ZarinpalPaymentConfiguration(
    string MerchantId,
    bool? IsSandbox,
    Uri? BaseUri,
    Uri? StartPayUri,
    Uri? CallbackUri);

public sealed record ZarinpalConfigurationValidation(bool IsValid, IReadOnlyList<string> Errors)
{
    public static ZarinpalConfigurationValidation Valid { get; } = new(true, Array.Empty<string>());
}

public interface IZarinpalPaymentConfigurationProvider
{
    Task<ZarinpalPaymentConfiguration> GetAsync(CancellationToken cancellationToken = default);
    Task<ZarinpalConfigurationValidation> ValidateAsync(CancellationToken cancellationToken = default);
}

public sealed class ZarinpalPaymentConfigurationProvider : IZarinpalPaymentConfigurationProvider
{
    public const string MerchantIdKey = "ZarinpalMerchantId";
    public const string SandboxKey = "ZarinpalSandbox";
    public const string BaseUrlKey = "ZarinpalBaseUrl";
    public const string StartPayUrlKey = "ZarinpalStartPayUrl";
    public const string CallbackUrlKey = "ZarinpalCallbackUrl";

    private readonly ISettingService _settings;
    private readonly IHostEnvironment _environment;

    public ZarinpalPaymentConfigurationProvider(
        ISettingService settings,
        IHostEnvironment environment)
    {
        _settings = settings;
        _environment = environment;
    }

    public async Task<ZarinpalPaymentConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        // ISettingService is backed by the scoped application DbContext.  Starting the
        // independent queries concurrently causes EF Core to reject the second query
        // under real checkout load.  Keep these reads ordered so a payment initiation
        // is reliable even when the settings service is database-backed.
        var merchantId = await _settings.GetValueAsync(MerchantIdKey);
        var sandboxValue = await _settings.GetValueAsync(SandboxKey);
        var baseUrl = await _settings.GetValueAsync(BaseUrlKey);
        var startPayUrl = await _settings.GetValueAsync(StartPayUrlKey);
        var callbackUrl = await _settings.GetValueAsync(CallbackUrlKey);

        return new ZarinpalPaymentConfiguration(
            merchantId?.Trim() ?? string.Empty,
            bool.TryParse(sandboxValue, out var sandbox) ? sandbox : null,
            ParseAbsoluteHttpsUri(baseUrl),
            ParseAbsoluteHttpsUri(startPayUrl),
            ParseAbsoluteHttpsUri(callbackUrl));
    }

    public async Task<ZarinpalConfigurationValidation> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await GetAsync(cancellationToken);
        return ZarinpalPaymentConfigurationRules.Validate(configuration, _environment.IsProduction());
    }

    private static Uri? ParseAbsoluteHttpsUri(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) &&
        string.IsNullOrEmpty(uri.UserInfo)
            ? uri
            : null;
}

public static class ZarinpalPaymentConfigurationRules
{
    private const string SandboxHost = "sandbox.zarinpal.com";
    private const string ProductionHost = "payment.zarinpal.com";

    /// <summary>
    /// A blank value or Guid.Empty is the installation-time sentinel. It is never
    /// a usable merchant account and callers must prevent it from reaching Zarinpal.
    /// </summary>
    public static bool IsDeploymentPlaceholder(string? merchantId) =>
        string.IsNullOrWhiteSpace(merchantId) ||
        (Guid.TryParse(merchantId, out var parsedMerchantId) && parsedMerchantId == Guid.Empty);

    public static ZarinpalConfigurationValidation Validate(
        ZarinpalPaymentConfiguration configuration,
        bool production)
    {
        var errors = new List<string>();
        if (!Guid.TryParse(configuration.MerchantId, out _))
            errors.Add("Zarinpal merchant ID must be a UUID.");
        if (configuration.IsSandbox is null)
            errors.Add("ZarinpalSandbox must be explicitly true or false.");
        if (configuration.BaseUri is null || configuration.StartPayUri is null || configuration.CallbackUri is null)
            errors.Add("Zarinpal base, start-payment, and callback URLs must be absolute HTTPS URLs without credentials.");

        if (configuration.IsSandbox is { } sandbox && configuration.BaseUri is not null && configuration.StartPayUri is not null)
        {
            var expectedHost = sandbox ? SandboxHost : ProductionHost;
            if (!HostEquals(configuration.BaseUri, expectedHost) || !HostEquals(configuration.StartPayUri, expectedHost))
                errors.Add(sandbox
                    ? "Sandbox mode must use sandbox.zarinpal.com for both gateway endpoints."
                    : "Live mode must use payment.zarinpal.com for both gateway endpoints.");
            if (sandbox && production)
                errors.Add("Production cannot use Zarinpal sandbox mode.");
        }

        if (configuration.CallbackUri is not null && !string.Equals(configuration.CallbackUri.AbsolutePath, "/api/payments/zarinpal/callback", StringComparison.OrdinalIgnoreCase))
            errors.Add("Zarinpal callback URL must target /api/payments/zarinpal/callback.");

        return errors.Count == 0
            ? ZarinpalConfigurationValidation.Valid
            : new ZarinpalConfigurationValidation(false, errors);
    }

    private static bool HostEquals(Uri uri, string host) =>
        string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase) && uri.Port == 443;

}
