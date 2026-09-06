using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms;

/// <summary>
/// Direct HTTP adapter for Asanak REST v2. Credentials are sent only in the
/// provider request body and are never logged or exposed through the admin API.
/// </summary>
public sealed class AsanakSmsSender : ISmsSender
{
    internal const string HttpClientName = "Asanak";
    private const int SuccessStatus = 200;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AsanakSmsSender> _logger;

    public AsanakSmsSender(IHttpClientFactory httpClientFactory, ILogger<AsanakSmsSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<SmsSendResult> SendVerifyAsync(
        SmsOptions options,
        string mobile,
        int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!options.HasAsanakCredentials)
            return Task.FromResult(SmsSendResult.Failure(SmsFailureReason.NotConfigured));

        // SmsService validates and canonicalizes its named contract parameters before the
        // transport is called; Asanak expects the resulting values as a positional array.
        var values = parameters.Select(x => x.Value).ToArray();
        return SendAsync(
            "webservice/v2rest/template",
            new TemplateRequest(
                options.AsanakUsername!,
                options.AsanakPassword!,
                templateId,
                mobile,
                values,
                1),
            response => SmsSendResult.Success(
                response.Data is { Length: > 0 } ? response.Data[0].ToString() : null,
                providerStatus: response.Meta?.Status,
                providerMessage: response.Meta?.Message),
            cancellationToken);
    }

    public Task<SmsSendResult> SendBulkAsync(
        SmsOptions options,
        string text,
        string mobile,
        CancellationToken cancellationToken = default)
    {
        if (!options.HasAsanakCredentials)
            return Task.FromResult(SmsSendResult.Failure(SmsFailureReason.NotConfigured));
        if (string.IsNullOrWhiteSpace(options.AsanakSource))
            return Task.FromResult(SmsSendResult.Failure(SmsFailureReason.InvalidLineNumber));

        return SendAsync(
            "webservice/v2rest/sendsms",
            new SendSmsRequest(
                options.AsanakUsername!,
                options.AsanakPassword!,
                options.AsanakSource,
                mobile,
                text,
                1),
            response => SmsSendResult.Success(
                response.Data is { Length: > 0 } ? response.Data[0].ToString() : null,
                providerStatus: response.Meta?.Status,
                providerMessage: response.Meta?.Message),
            cancellationToken);
    }

    public async Task<SmsAccountStatus> GetAccountStatusAsync(
        SmsOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.HasAsanakCredentials)
            return new SmsAccountStatus { IsSuccess = false, UserMessage = "نام کاربری یا رمز وب‌سرویس آسانک تنظیم نشده است." };

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = CreateRequest(
                "webservice/v2rest/getcredit",
                new CredentialsRequest(options.AsanakUsername!, options.AsanakPassword!));
            using var response = await client.SendAsync(request, cancellationToken);
            var provider = await ReadResponseAsync<CreditResponse>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || !provider.IsSuccessful)
            {
                var reason = MapFailure(response.StatusCode, provider.Meta?.Status, provider.Meta?.Message);
                _logger.LogWarning("Asanak account status request failed. Reason={Reason}", reason);
                return new SmsAccountStatus { IsSuccess = false, UserMessage = FriendlyMessage(reason) };
            }

            IReadOnlyList<long> lines = long.TryParse(options.AsanakSource, out var source)
                ? [source]
                : [];
            return new SmsAccountStatus { IsSuccess = true, Credit = provider.Data?.Credit, Lines = lines };
        }
        catch (Exception ex)
        {
            var mapped = MapException(ex);
            _logger.LogWarning("Asanak account status request failed. Reason={Reason}", mapped.FailureReason);
            return new SmsAccountStatus { IsSuccess = false, UserMessage = FriendlyMessage(mapped.FailureReason) };
        }
    }

    private async Task<SmsSendResult> SendAsync<TRequest>(
        string path,
        TRequest payload,
        Func<AsanakResponse<long[]>, SmsSendResult> success,
        CancellationToken cancellationToken)
    {
        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = CreateRequest(path, payload);
            using var response = await client.SendAsync(request, cancellationToken);
            var provider = await ReadResponseAsync<long[]>(response, cancellationToken);

            if (!response.IsSuccessStatusCode || !provider.IsSuccessful)
            {
                return SmsSendResult.Failure(
                    MapFailure(response.StatusCode, provider.Meta?.Status, provider.Meta?.Message),
                    providerStatus: provider.Meta?.Status,
                    providerMessage: provider.Meta?.Message);
            }

            return success(provider);
        }
        catch (Exception ex)
        {
            return MapException(ex);
        }
    }

    private static HttpRequestMessage CreateRequest<TRequest>(string path, TRequest payload)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload, options: JsonOptions)
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static async Task<AsanakResponse<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<AsanakResponse<T>>(JsonOptions, cancellationToken)
            ?? new AsanakResponse<T>(null, default);

    private static SmsSendResult MapException(Exception ex)
    {
        var reason = ex switch
        {
            OperationCanceledException => SmsFailureReason.Timeout,
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized } => SmsFailureReason.Unauthorized,
            HttpRequestException => SmsFailureReason.Network,
            JsonException => SmsFailureReason.ProviderUnavailable,
            _ => SmsFailureReason.Unknown
        };
        return SmsSendResult.Failure(reason, providerMessage: ex.Message);
    }

    private static SmsFailureReason MapFailure(HttpStatusCode statusCode, int? providerStatus, string? message)
    {
        if (statusCode == HttpStatusCode.Unauthorized || providerStatus == 1015)
            return SmsFailureReason.Unauthorized;
        if (statusCode == HttpStatusCode.PaymentRequired || providerStatus is 1005 or 1006)
            return SmsFailureReason.InsufficientCredit;
        if (statusCode == HttpStatusCode.TooManyRequests || providerStatus == 429)
            return SmsFailureReason.TooManyRequests;
        if (statusCode == HttpStatusCode.Forbidden || providerStatus == 1013)
            return SmsFailureReason.AccessDenied;
        if (statusCode == HttpStatusCode.NotAcceptable)
            return providerStatus == 1010 ? SmsFailureReason.InvalidMobile : SmsFailureReason.InvalidLineNumber;
        if (statusCode == HttpStatusCode.BadRequest)
            return ClassifyMessage(message) ?? SmsFailureReason.InvalidParameter;
        if ((int)statusCode >= 500)
            return SmsFailureReason.ProviderUnavailable;
        return ClassifyMessage(message) ?? SmsFailureReason.Unknown;
    }

    private static SmsFailureReason? ClassifyMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var value = message.ToLowerInvariant();
        if (value.Contains("username") || value.Contains("password") || value.Contains("رمز") || value.Contains("نام کاربری")) return SmsFailureReason.Unauthorized;
        if (value.Contains("credit") || value.Contains("اعتبار")) return SmsFailureReason.InsufficientCredit;
        if (value.Contains("template") || value.Contains("قالب")) return SmsFailureReason.InvalidTemplate;
        if (value.Contains("parameter") || value.Contains("پارامتر")) return SmsFailureReason.InvalidParameter;
        if (value.Contains("source") || value.Contains("مبدا") || value.Contains("مبدأ")) return SmsFailureReason.InvalidLineNumber;
        if (value.Contains("destination") || value.Contains("mobile") || value.Contains("گیرنده") || value.Contains("موبایل")) return SmsFailureReason.InvalidMobile;
        return null;
    }

    private static string FriendlyMessage(SmsFailureReason reason) => reason switch
    {
        SmsFailureReason.Unauthorized => "نام کاربری یا رمز وب‌سرویس آسانک نامعتبر است.",
        SmsFailureReason.InsufficientCredit => "اعتبار حساب پیامک کافی نیست.",
        SmsFailureReason.TooManyRequests => "تعداد درخواست‌ها بیش از حد مجاز است.",
        SmsFailureReason.InvalidLineNumber => "شماره مبدأ پیامکی معتبر یا فعال نیست.",
        SmsFailureReason.Network or SmsFailureReason.Timeout or SmsFailureReason.ProviderUnavailable => "ارتباط با سرویس پیامک برقرار نشد.",
        _ => "بررسی وضعیت حساب پیامک ناموفق بود."
    };

    private sealed record CredentialsRequest(string Username, string Password);
    private sealed record SendSmsRequest(
        string Username,
        string Password,
        [property: JsonPropertyName("source")] string Source,
        [property: JsonPropertyName("destination")] string Destination,
        [property: JsonPropertyName("message")] string Message,
        [property: JsonPropertyName("send_to_blacklist")] int SendToBlacklist);
    private sealed record TemplateRequest(
        string Username,
        string Password,
        [property: JsonPropertyName("template_id")] int TemplateId,
        [property: JsonPropertyName("destination")] string Destination,
        [property: JsonPropertyName("parameters")] IReadOnlyList<string> Parameters,
        [property: JsonPropertyName("send_to_blacklist")] int SendToBlacklist);
    private sealed record CreditResponse(decimal? Credit);
    private sealed record AsanakMeta(int? Status, string? Message);
    private sealed record AsanakResponse<T>(AsanakMeta? Meta, T? Data)
    {
        public bool IsSuccessful => Meta?.Status == SuccessStatus;
    }
}
