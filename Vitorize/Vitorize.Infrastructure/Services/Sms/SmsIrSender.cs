using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms;

/// <summary>
/// Direct HTTP adapter for SMS.ir. The provider SDK is intentionally not used: all requests go
/// through the documented v1 API with the current API key and line/template settings.
/// </summary>
public sealed class SmsIrSender : ISmsSender
{
    internal const string HttpClientName = "SmsIr";
    private const int SuccessStatus = 1;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmsIrSender> _logger;

    public SmsIrSender(IHttpClientFactory httpClientFactory, ILogger<SmsIrSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public Task<SmsSendResult> SendVerifyAsync(
        string apiKey,
        string mobile,
        int templateId,
        IReadOnlyList<SmsTemplateParameter> parameters,
        CancellationToken cancellationToken = default) =>
        SendAsync<VerifyRequest, VerifyResponse>(
            apiKey,
            "v1/send/verify",
            new VerifyRequest(
                mobile,
                templateId,
                parameters.Select(x => new VerifyParameter(x.Name, x.Value)).ToArray()),
            response => SmsSendResult.Success(
                response.Data?.MessageId.ToString(), response.Data?.Cost, response.Status, response.Message),
            cancellationToken);

    public Task<SmsSendResult> SendBulkAsync(
        string apiKey,
        long lineNumber,
        string text,
        string mobile,
        CancellationToken cancellationToken = default)
    {
        if (lineNumber <= 0)
            return Task.FromResult(SmsSendResult.Failure(SmsFailureReason.InvalidLineNumber));

        return SendAsync<BulkRequest, BulkResponse>(
            apiKey,
            "v1/send/bulk",
            new BulkRequest(lineNumber, text, [mobile], null),
            response =>
            {
                var messageId = response.Data?.MessageIds?.FirstOrDefault().ToString()
                    ?? response.Data?.PackId?.ToString();
                return SmsSendResult.Success(messageId, response.Data?.Cost, response.Status, response.Message);
            },
            cancellationToken);
    }

    public async Task<SmsAccountStatus> GetAccountStatusAsync(
        string apiKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return new SmsAccountStatus { IsSuccess = false, UserMessage = "کلید API تنظیم نشده است." };

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var creditResponse = await SendGetAsync(client, "v1/credit", apiKey, cancellationToken);
            var credit = await ReadResponseAsync<decimal>(creditResponse, cancellationToken);
            if (!credit.IsSuccessful)
                return FailedAccountStatus(credit);

            using var linesResponse = await SendGetAsync(client, "v1/line", apiKey, cancellationToken);
            var lines = await ReadResponseAsync<long[]>(linesResponse, cancellationToken);
            if (!lines.IsSuccessful)
                return FailedAccountStatus(lines);

            return new SmsAccountStatus { IsSuccess = true, Credit = credit.Data, Lines = lines.Data ?? [] };
        }
        catch (Exception ex)
        {
            var mapped = MapException(ex);
            _logger.LogWarning("SMS.ir account status request failed. Reason={Reason}", mapped.FailureReason);
            return new SmsAccountStatus { IsSuccess = false, UserMessage = FriendlyMessage(mapped.FailureReason) };
        }
    }

    private async Task<SmsSendResult> SendAsync<TRequest, TResponse>(
        string apiKey,
        string path,
        TRequest payload,
        Func<SmsIrResponse<TResponse>, SmsSendResult> success,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
            return SmsSendResult.Failure(SmsFailureReason.NotConfigured);

        try
        {
            var client = _httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Post, path)
            {
                Content = JsonContent.Create(payload, options: JsonOptions)
            };
            request.Headers.Add("x-api-key", apiKey);

            using var response = await client.SendAsync(request, cancellationToken);
            var provider = await ReadResponseAsync<TResponse>(response, cancellationToken);

            if (!provider.IsSuccessful)
                return SmsSendResult.Failure(
                    MapFailure(response.StatusCode, provider.Message),
                    providerStatus: provider.Status,
                    providerMessage: provider.Message);

            return success(provider);
        }
        catch (Exception ex)
        {
            return MapException(ex);
        }
    }

    private static async Task<HttpResponseMessage> SendGetAsync(
        HttpClient client,
        string path,
        string apiKey,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("x-api-key", apiKey);
        return await client.SendAsync(request, cancellationToken);
    }

    private static async Task<SmsIrResponse<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken) =>
        await response.Content.ReadFromJsonAsync<SmsIrResponse<T>>(JsonOptions, cancellationToken)
            ?? new SmsIrResponse<T>(null, null, default);

    private static SmsAccountStatus FailedAccountStatus<T>(SmsIrResponse<T> response) =>
        new() { IsSuccess = false, UserMessage = FriendlyMessage(MapFailure(null, response.Message)) };

    private static SmsSendResult MapException(Exception ex)
    {
        var reason = ex switch
        {
            OperationCanceledException => SmsFailureReason.Timeout,
            HttpRequestException { StatusCode: HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden } => SmsFailureReason.Unauthorized,
            HttpRequestException => SmsFailureReason.Network,
            JsonException => SmsFailureReason.ProviderUnavailable,
            _ => SmsFailureReason.Unknown
        };
        return SmsSendResult.Failure(reason, providerMessage: ex.Message);
    }

    private static SmsFailureReason MapFailure(HttpStatusCode? statusCode, string? message)
    {
        if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) return SmsFailureReason.Unauthorized;
        if (statusCode == HttpStatusCode.TooManyRequests) return SmsFailureReason.TooManyRequests;
        if (statusCode is HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity) return ClassifyMessage(message) ?? SmsFailureReason.InvalidParameter;
        if (statusCode is not null && (int)statusCode >= 500) return SmsFailureReason.ProviderUnavailable;
        return ClassifyMessage(message) ?? SmsFailureReason.Unknown;
    }

    private static SmsFailureReason? ClassifyMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return null;
        var value = message.ToLowerInvariant();
        if (value.Contains("اعتبار") || value.Contains("credit") || value.Contains("موجودی")) return SmsFailureReason.InsufficientCredit;
        if (value.Contains("قالب") || value.Contains("template")) return SmsFailureReason.InvalidTemplate;
        if (value.Contains("پارامتر") || value.Contains("parameter")) return SmsFailureReason.InvalidParameter;
        if (value.Contains("خط") || value.Contains("line")) return SmsFailureReason.InvalidLineNumber;
        if (value.Contains("موبایل") || value.Contains("mobile") || value.Contains("شماره")) return SmsFailureReason.InvalidMobile;
        if (value.Contains("کلید") || value.Contains("api key") || value.Contains("unauthor")) return SmsFailureReason.Unauthorized;
        return null;
    }

    private static string FriendlyMessage(SmsFailureReason reason) => reason switch
    {
        SmsFailureReason.Unauthorized => "کلید API نامعتبر است.",
        SmsFailureReason.InsufficientCredit => "اعتبار حساب پیامک کافی نیست.",
        SmsFailureReason.TooManyRequests => "تعداد درخواست‌ها بیش از حد مجاز است.",
        SmsFailureReason.InvalidLineNumber => "شماره خط پیامکی معتبر نیست.",
        SmsFailureReason.Network or SmsFailureReason.Timeout or SmsFailureReason.ProviderUnavailable => "ارتباط با سرویس پیامک برقرار نشد.",
        _ => "بررسی وضعیت حساب پیامک ناموفق بود."
    };

    private sealed record VerifyRequest(string Mobile, int TemplateId, IReadOnlyList<VerifyParameter> Parameters);
    private sealed record VerifyParameter(string Name, string Value);
    private sealed record BulkRequest(long LineNumber, string MessageText, IReadOnlyList<string> Mobiles, long? SendDateTime);
    private sealed record VerifyResponse(long? MessageId, decimal? Cost);
    private sealed record BulkResponse(Guid? PackId, long[]? MessageIds, decimal? Cost);
    private sealed record SmsIrResponse<T>(int? Status, string? Message, T? Data)
    {
        public bool IsSuccessful => Status == SuccessStatus;
    }
}
