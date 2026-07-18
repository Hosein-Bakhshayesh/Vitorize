using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal.Models;
using Vitorize.Infrastructure.Services.Testing;

namespace Vitorize.Infrastructure.Services
{
    public class ZarinpalGatewayService : IZarinpalGatewayService
    {
        private readonly HttpClient _httpClient;
        private readonly ISettingService _settingService;
        private readonly IHostEnvironment _environment;
        private readonly IOptionsMonitor<TestingFaultInjectionOptions> _faults;

        public ZarinpalGatewayService(
            HttpClient httpClient,
            ISettingService settingService,
            IHostEnvironment environment,
            IOptionsMonitor<TestingFaultInjectionOptions> faults)
        {
            _httpClient = httpClient;
            _settingService = settingService;
            _environment = environment;
            _faults = faults;
        }

        // Testing-environment-only fault injection. Impossible outside "Testing"; Off by default.
        private bool PaymentFaultEnabled(string mode) =>
            _environment.IsEnvironment("Testing") &&
            _faults.CurrentValue.IsPaymentFaultRequested &&
            _faults.CurrentValue.Payment.Trim().Equals(mode, StringComparison.OrdinalIgnoreCase);

        public async Task<(bool Success, string Authority, string PaymentUrl)> CreatePaymentAsync(
            decimal amount,
            string description,
            string? mobile = null,
            string? email = null,
            string? orderId = null)
        {
            if (PaymentFaultEnabled("CreateFail"))
                return (false, string.Empty, string.Empty);

            var merchantId = await GetSettingOrDefaultAsync("ZarinpalMerchantId", string.Empty);

            // No real gateway configured. In development we return a mock authority with an empty
            // payment URL so the internal mock-verify flow can complete the order (enabling local
            // end-to-end testing). In production we must NOT silently "succeed" — that would let
            // orders complete without a real payment — so we degrade to a failure that surfaces a
            // friendly "gateway unavailable" message instead of an unhandled 500.
            if (string.IsNullOrWhiteSpace(merchantId))
                return (_environment.IsDevelopment() || _environment.IsEnvironment("Testing"))
                    ? (true, $"MOCK-{Guid.NewGuid():N}", string.Empty)
                    : (false, string.Empty, string.Empty);

            try
            {
                var callbackUrl = await GetRequiredSettingAsync("ZarinpalCallbackUrl");

                var baseUrl = await GetSettingOrDefaultAsync(
                    "ZarinpalBaseUrl",
                    "https://sandbox.zarinpal.com/pg/v4/payment");

                var startPayUrl = await GetSettingOrDefaultAsync(
                    "ZarinpalStartPayUrl",
                    "https://sandbox.zarinpal.com/pg/StartPay");

                baseUrl = baseUrl.TrimEnd('/');
                startPayUrl = startPayUrl.TrimEnd('/');

                var request = new ZarinpalRequestDto
                {
                    merchant_id = merchantId,
                    amount = amount,
                    currency = "IRT",
                    description = description,
                    callback_url = callbackUrl,
                    metadata = new ZarinpalMetadataDto
                    {
                        mobile = mobile,
                        email = email,
                        order_id = orderId
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"{baseUrl}/request.json",
                    request);

                var responseText = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return (false, string.Empty, string.Empty);

                var result = Deserialize<ZarinpalRequestResultDto>(responseText);

                if (result?.data == null ||
                    result.data.code != 100 ||
                    string.IsNullOrWhiteSpace(result.data.authority))
                {
                    return (false, string.Empty, string.Empty);
                }

                return (
                    true,
                    result.data.authority,
                    $"{startPayUrl}/{result.data.authority}"
                );
            }
            catch
            {
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

            var merchantId = await GetRequiredSettingAsync("ZarinpalMerchantId");

            var baseUrl = await GetSettingOrDefaultAsync(
                "ZarinpalBaseUrl",
                "https://sandbox.zarinpal.com/pg/v4/payment");

            baseUrl = baseUrl.TrimEnd('/');

            var request = new ZarinpalVerifyRequestDto
            {
                merchant_id = merchantId,
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

        private async Task<string> GetRequiredSettingAsync(string key)
        {
            var value = await _settingService.GetValueAsync(key);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Setting '{key}' is not configured.");

            return value.Trim();
        }

        private async Task<string> GetSettingOrDefaultAsync(string key, string defaultValue)
        {
            var value = await _settingService.GetValueAsync(key);

            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value.Trim();
        }

        public async Task<string> BuildPaymentUrlAsync(string authority)
        {
            var startPayUrl = await GetSettingOrDefaultAsync(
                "ZarinpalStartPayUrl",
                "https://sandbox.zarinpal.com/pg/StartPay");

            startPayUrl = startPayUrl.TrimEnd('/');

            return $"{startPayUrl}/{authority}";
        }
    }
}
