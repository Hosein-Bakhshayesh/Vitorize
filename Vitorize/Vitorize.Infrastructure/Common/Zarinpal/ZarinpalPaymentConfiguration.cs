using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Vitorize.Application.Interfaces;

namespace Vitorize.Infrastructure.Common.Zarinpal;

/// <summary>
/// The single authoritative gateway configuration. Values are intentionally loaded from the
/// protected Payment settings group; only the public API origin is host configuration.
/// </summary>
public sealed record ZarinpalPaymentConfiguration(
    string MerchantId,
    bool? IsSandbox,
    Uri? BaseUri,
    Uri? StartPayUri,
    Uri? CallbackUri,
    Uri? PublicOrigin);

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
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public ZarinpalPaymentConfigurationProvider(
        ISettingService settings,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        _settings = settings;
        _configuration = configuration;
        _environment = environment;
    }

    public async Task<ZarinpalPaymentConfiguration> GetAsync(CancellationToken cancellationToken = default)
    {
        var values = await Task.WhenAll(
            _settings.GetValueAsync(MerchantIdKey),
            _settings.GetValueAsync(SandboxKey),
            _settings.GetValueAsync(BaseUrlKey),
            _settings.GetValueAsync(StartPayUrlKey),
            _settings.GetValueAsync(CallbackUrlKey));

        return new ZarinpalPaymentConfiguration(
            values[0]?.Trim() ?? string.Empty,
            bool.TryParse(values[1], out var sandbox) ? sandbox : null,
            ParseAbsoluteHttpsUri(values[2]),
            ParseAbsoluteHttpsUri(values[3]),
            ParseAbsoluteHttpsUri(values[4]),
            ParseAbsoluteHttpsUri(_configuration["Hosting:PublicOrigin"]));
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

        if (production)
        {
            if (configuration.PublicOrigin is null)
                errors.Add("Hosting:PublicOrigin must be configured as an HTTPS origin in Production.");
            else if (configuration.CallbackUri is not null && !SameOrigin(configuration.CallbackUri, configuration.PublicOrigin))
                errors.Add("Zarinpal callback origin must match Hosting:PublicOrigin.");
        }

        return errors.Count == 0
            ? ZarinpalConfigurationValidation.Valid
            : new ZarinpalConfigurationValidation(false, errors);
    }

    private static bool HostEquals(Uri uri, string host) =>
        string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase) && uri.Port == 443;

    private static bool SameOrigin(Uri left, Uri right) =>
        string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) && left.Port == right.Port;
}
