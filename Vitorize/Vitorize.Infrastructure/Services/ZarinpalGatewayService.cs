using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using Vitorize.Application.Interfaces;
using Vitorize.Infrastructure.Common.Zarinpal;
using Vitorize.Infrastructure.Common.Zarinpal.Models;

namespace Vitorize.Infrastructure.Services
{
    public class ZarinpalGatewayService : IZarinpalGatewayService
    {
        private readonly HttpClient _httpClient;
        private readonly ZarinpalSettings _settings;

        public ZarinpalGatewayService(
            HttpClient httpClient,
            IOptions<ZarinpalSettings> settings)
        {
            _httpClient = httpClient;
            _settings = settings.Value;
        }

        public async Task<(bool Success,
            string Authority,
            string PaymentUrl)>
            CreatePaymentAsync(
                decimal amount,
                string description)
        {
            var request = new ZarinpalRequestDto
            {
                merchant_id = _settings.MerchantId,
                amount = amount,
                callback_url = _settings.CallbackUrl,
                description = description
            };

            var response =
                await _httpClient.PostAsJsonAsync(
                    $"{_settings.BaseUrl}/request.json",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<ZarinpalRequestResultDto>();

            if (result?.data == null ||
                result.data.code != 100)
            {
                return (false, string.Empty, string.Empty);
            }

            return (
                true,
                result.data.authority,
                $"{_settings.StartPayUrl}/{result.data.authority}"
            );
        }

        public async Task<(bool Success,
            long RefId)>
            VerifyPaymentAsync(
                string authority,
                decimal amount)
        {
            var request = new ZarinpalVerifyRequestDto
            {
                merchant_id = _settings.MerchantId,
                amount = amount,
                authority = authority
            };

            var response =
                await _httpClient.PostAsJsonAsync(
                    $"{_settings.BaseUrl}/verify.json",
                    request);

            var result =
                await response.Content
                    .ReadFromJsonAsync<ZarinpalVerifyResultDto>();

            if (result?.data == null)
                return (false, 0);

            if (result.data.code != 100 &&
                result.data.code != 101)
            {
                return (false, 0);
            }

            return (
                true,
                result.data.ref_id
            );
        }
    }
}