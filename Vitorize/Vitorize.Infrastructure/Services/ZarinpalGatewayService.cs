using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Common.Zarinpal.Models;
using Vitorize.Infrastructure.Services.Testing;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class ZarinpalGatewayService : IZarinpalGatewayService
    {
        private readonly HttpClient _httpClient;
        private readonly IZarinpalPaymentConfigurationProvider _configurationProvider;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<TestingFaultInjectionOptions> _faults;
        private readonly ILogger<ZarinpalGatewayService> _logger;

        public ZarinpalGatewayService(
            HttpClient httpClient,
            IZarinpalPaymentConfigurationProvider configurationProvider,
            IHostEnvironment environment,
            IOptionsMonitor<TestingFaultInjectionOptions> faults,
            ILogger<ZarinpalGatewayService> logger)
        {
            _httpClient = httpClient;
            _configurationProvider = configurationProvider;
            _environment = environment;
            _faults = faults;
            _logger = logger;
        }

        /// <summary>
        /// A gateway that refuses to start a payment because its own settings are inconsistent looks
        /// exactly like an unreachable provider in the log, which leaves an operator with no way to
        /// find the offending setting. Report which rule failed - names only, never the merchant id.
        /// </summary>
        private static string Truncate(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "(empty)"
                : value.Length <= 500 ? value : value[..500] + "...(truncated)";

        private void LogInvalidConfiguration(string operation, IEnumerable<string> errors) =>
            _logger.LogWarning(
                "Zarinpal {Operation} refused: the stored gateway configuration is invalid. Fix these settings in admin settings: {Errors}",
                operation, string.Join(" | ", errors));

        // Testing-environment-only fault injection. Impossible outside "Testing"; Off by default.
        private bool PaymentFaultEnabled(string mode) =>
            _environment.IsEnvironment("Testing") &&
            _faults.CurrentValue.IsPaymentFaultRequested &&
            _faults.CurrentValue.Payment.Trim().Equals(mode, StringComparison.OrdinalIgnoreCase);

        public async Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(
            decimal amount,
            CurrencyType currency,
            string description,
            string? mobile = null,
            string? email = null,
            string? orderId = null)
        {
            if (PaymentFaultEnabled("CreateFail"))
                return (false, string.Empty, string.Empty);

            var configuration = await _configurationProvider.GetAsync();

            // No real gateway configured. In development we return a mock authority with an empty
            // payment URL so the internal mock-verify flow can complete the order (enabling local
            // end-to-end testing). In production we must NOT silently "succeed" — that would let
            // orders complete without a real payment — so we degrade to a failure that surfaces a
            // friendly "gateway unavailable" message instead of an unhandled 500.
            if (ZarinpalPaymentConfigurationRules.IsDeploymentPlaceholder(configuration.MerchantId))
            {
                if (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
                    return (true, $"MOCK-{Guid.NewGuid():N}", string.Empty);
                LogInvalidConfiguration("payment request", new[] { "ZarinpalMerchantId is still the installation placeholder." });
                return (false, string.Empty, string.Empty);
            }

            try
            {
                var validation = await _configurationProvider.ValidateAsync();
                if (!validation.IsValid || configuration.BaseUri is null || configuration.StartPayUri is null || configuration.CallbackUri is null)
                {
                    LogInvalidConfiguration("payment request", validation.Errors);
                    return (false, string.Empty, string.Empty);
                }

                var baseUrl = configuration.BaseUri.AbsoluteUri.TrimEnd('/');
                var startPayUrl = configuration.StartPayUri.AbsoluteUri.TrimEnd('/');

                var request = new ZarinpalRequestDto
                {
                    merchant_id = configuration.MerchantId,
                    amount = amount,
                    currency = currency switch
                    {
                        CurrencyType.Toman => "IRT",
                        CurrencyType.Rial => "IRR",
                        _ => throw new InvalidOperationException("Unsupported payment currency.")
                    },
                    description = description,
                    callback_url = configuration.CallbackUri.AbsoluteUri,
                    metadata = new ZarinpalMetadataDto
                    {
                        mobile = mobile,
                        email = email,
                        order_id = orderId
                    }
                };

                var requestUri = $"{baseUrl}/request.json";
                var clock = System.Diagnostics.Stopwatch.StartNew();
                var response = await _httpClient.PostAsJsonAsync(requestUri, request);

                var responseText = await response.Content.ReadAsStringAsync();
                clock.Stop();
                var host = new Uri(requestUri).Host;
                var path = new Uri(requestUri).AbsolutePath;

                if (!response.IsSuccessStatusCode)
                {
                    // The provider's own explanation is the only thing that identifies a rejected
                    // merchant, a disabled currency or a bad amount. Losing it leaves nothing but a
                    // generic REQUEST_FAILED, so record it - it carries no secret of ours.
                    _logger.LogWarning(
                        "Zarinpal payment request rejected. EventType=ZarinpalRequestRejected RequestHost={Host} RequestPath={Path} HttpStatusCode={Status} ElapsedMs={Elapsed} AuthorityPresent=false ProviderResponse={Response}",
                        host, path, (int)response.StatusCode, clock.ElapsedMilliseconds, Truncate(responseText));
                    return (false, string.Empty, string.Empty);
                }

                var result = Deserialize<ZarinpalRequestResultDto>(responseText);

                if (result?.data == null ||
                    result.data.code != 100 ||
                    string.IsNullOrWhiteSpace(result.data.authority))
                {
                    _logger.LogWarning(
                        "Zarinpal payment request returned no usable authority. EventType=ZarinpalRequestNoAuthority RequestHost={Host} RequestPath={Path} HttpStatusCode={Status} ProviderCode={Code} ElapsedMs={Elapsed} AuthorityPresent=false ProviderResponse={Response}",
                        host, path, (int)response.StatusCode, result?.data?.code, clock.ElapsedMilliseconds, Truncate(responseText));
                    return (false, string.Empty, string.Empty);
                }

                _logger.LogInformation(
                    "Zarinpal payment request accepted. EventType=ZarinpalRequestAccepted RequestHost={Host} RequestPath={Path} HttpStatusCode={Status} ProviderCode={Code} ElapsedMs={Elapsed} AuthorityPresent=true",
                    host, path, (int)response.StatusCode, result.data.code, clock.ElapsedMilliseconds);

                return (
                    true,
                    result.data.authority,
                    $"{startPayUrl}/{result.data.authority}"
                );
            }
            catch (Exception exception)
            {
                // Egress blocked, DNS, TLS or timeout all land here and were previously invisible.
                _logger.LogWarning(exception,
                    "Zarinpal payment request could not be sent. EventType=ZarinpalRequestTransportFailure RequestHost={Host} ExceptionType={ExceptionType} AuthorityPresent=false",
                    "payment.zarinpal.com", exception.GetType().Name);
                // Network / gateway failure — degrade gracefully so the caller surfaces a
                // friendly "gateway unavailable" message instead of an unhandled 500.
                return (false, string.Empty, string.Empty);
            }
        }

        public async Task<(bool Success, long RefId)> VerifyPaymentAsync(
            string authority,
            decimal amount)
        {
            if (PaymentFaultEnabled("VerifyFail"))
                return (false, 0);

            var configuration = await _configurationProvider.GetAsync();

            if (ZarinpalPaymentConfigurationRules.IsDeploymentPlaceholder(configuration.MerchantId))
                return (false, 0);

            var validation = await _configurationProvider.ValidateAsync();
            if (!validation.IsValid || configuration.BaseUri is null)
                return (false, 0);

            var baseUrl = configuration.BaseUri.AbsoluteUri.TrimEnd('/');

            var request = new ZarinpalVerifyRequestDto
            {
                merchant_id = configuration.MerchantId,
                amount = amount,
                authority = authority
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{baseUrl}/verify.json",
                request);

            var responseText = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return (false, 0);

            var result = Deserialize<ZarinpalVerifyResultDto>(responseText);

            if (result?.data == null)
                return (false, 0);

            if (result.data.code != 100 &&
                result.data.code != 101)
                return (false, 0);

            return (true, result.data.ref_id);
        }

    private static T? Deserialize<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(
                    json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch
            {
                return default;
            }
        }

        public async Task<string> BuildPaymentUrlAsync(string authority)
        {
            var configuration = await _configurationProvider.GetAsync();
            var validation = await _configurationProvider.ValidateAsync();
            if (!validation.IsValid || configuration.StartPayUri is null)
                throw new InvalidOperationException("Zarinpal payment configuration is invalid.");
            return $"{configuration.StartPayUri.AbsoluteUri.TrimEnd('/')}/{authority}";
        }

    }
}
