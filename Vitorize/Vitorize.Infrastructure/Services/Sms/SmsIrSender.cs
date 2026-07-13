using System.Collections.Concurrent;
using IPE.SmsIrClient;
using IPE.SmsIrClient.Exceptions;
using IPE.SmsIrClient.Models.Requests;
using Microsoft.Extensions.Logging;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms
{
    /// <summary>
    /// آداپتور رسمی SMS.ir (بسته IPE.SmsIr). تنها نقطه‌ای در کل راه‌حل است که SDK را مستقیماً صدا می‌زند.
    /// به‌صورت Singleton ثبت می‌شود و نمونه‌های <see cref="SmsIr"/> را برای بازاستفاده از HttpClient
    /// بر اساس کلید API کش می‌کند تا از socket exhaustion جلوگیری شود.
    /// خطاهای خام ارائه‌دهنده به <see cref="SmsFailureReason"/> نگاشت می‌شوند و هرگز به بیرون نشت نمی‌کنند.
    /// </summary>
    public sealed class SmsIrSender : ISmsSender
    {
        private const byte SmsIrSuccessStatus = 1;

        private readonly ILogger<SmsIrSender> _logger;
        private readonly ConcurrentDictionary<string, SmsIr> _clients = new();

        public SmsIrSender(ILogger<SmsIrSender> logger)
        {
            _logger = logger;
        }

        private SmsIr GetClient(string apiKey) =>
            _clients.GetOrAdd(apiKey, key => new SmsIr(key));

        public async Task<SmsSendResult> SendVerifyAsync(
            string apiKey,
            string mobile,
            int templateId,
            IReadOnlyList<SmsTemplateParameter> parameters,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return SmsSendResult.Failure(SmsFailureReason.NotConfigured);

            try
            {
                var client = GetClient(apiKey);

                var sdkParams = parameters
                    .Select(p => new VerifySendParameter(p.Name, p.Value))
                    .ToArray();

                var result = await client.VerifySendAsync(mobile, templateId, sdkParams);

                if (result is null)
                    return SmsSendResult.Failure(SmsFailureReason.Unknown);

                if (result.Status != SmsIrSuccessStatus)
                    return SmsSendResult.Failure(
                        MapStatus(result.Status, result.Message),
                        providerStatus: result.Status,
                        providerMessage: result.Message);

                return SmsSendResult.Success(
                    providerMessageId: result.Data?.MessageId.ToString(),
                    cost: result.Data?.Cost,
                    providerStatus: result.Status,
                    providerMessage: result.Message);
            }
            catch (Exception ex)
            {
                return MapException(ex);
            }
        }

        public async Task<SmsSendResult> SendBulkAsync(
            string apiKey,
            long lineNumber,
            string text,
            string mobile,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return SmsSendResult.Failure(SmsFailureReason.NotConfigured);

            if (lineNumber <= 0)
                return SmsSendResult.Failure(SmsFailureReason.InvalidLineNumber);

            try
            {
                var client = GetClient(apiKey);

                var result = await client.BulkSendAsync(lineNumber, text, new[] { mobile }, null);

                if (result is null)
                    return SmsSendResult.Failure(SmsFailureReason.Unknown);

                if (result.Status != SmsIrSuccessStatus)
                    return SmsSendResult.Failure(
                        MapStatus(result.Status, result.Message),
                        providerStatus: result.Status,
                        providerMessage: result.Message);

                var messageId = result.Data?.MessageIds is { Length: > 0 } ids && ids[0].HasValue
                    ? ids[0]!.Value.ToString()
                    : result.Data?.PackId.ToString();

                return SmsSendResult.Success(
                    providerMessageId: messageId,
                    cost: result.Data?.Cost,
                    providerStatus: result.Status,
                    providerMessage: result.Message);
            }
            catch (Exception ex)
            {
                return MapException(ex);
            }
        }

        public async Task<SmsAccountStatus> GetAccountStatusAsync(
            string apiKey,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
                return new SmsAccountStatus { IsSuccess = false, UserMessage = "کلید API تنظیم نشده است." };

            try
            {
                var client = GetClient(apiKey);

                var credit = await client.GetCreditAsync();
                var lines = await client.GetLinesAsync();

                return new SmsAccountStatus
                {
                    IsSuccess = true,
                    Credit = credit?.Data,
                    Lines = lines?.Data
                };
            }
            catch (Exception ex)
            {
                var mapped = MapException(ex);
                _logger.LogWarning(
                    "SMS account status check failed. Reason={Reason} Provider={ProviderMessage}",
                    mapped.FailureReason, mapped.ProviderMessage);

                return new SmsAccountStatus
                {
                    IsSuccess = false,
                    UserMessage = FriendlyMessage(mapped.FailureReason)
                };
            }
        }

        private static SmsFailureReason MapStatus(byte status, string? message)
        {
            // status != 1 → خطای منطقی؛ تلاش برای تشخیص از روی متن.
            return ClassifyMessage(message) ?? SmsFailureReason.Unknown;
        }

        private static SmsSendResult MapException(Exception ex)
        {
            var reason = ex switch
            {
                UnauthorizedException => SmsFailureReason.Unauthorized,
                AccessDeniedException => SmsFailureReason.AccessDenied,
                TooManyRequestException => SmsFailureReason.TooManyRequests,
                LogicalException le => ClassifyMessage(le.Message) ?? SmsFailureReason.Unknown,
                SmsIrException => SmsFailureReason.ProviderUnavailable,
                TaskCanceledException => SmsFailureReason.Timeout,
                TimeoutException => SmsFailureReason.Timeout,
                HttpRequestException => SmsFailureReason.Network,
                _ => SmsFailureReason.Unknown
            };

            // پیام خام ارائه‌دهنده فقط برای لاگ داخلی نگهداری می‌شود.
            return SmsSendResult.Failure(reason, providerMessage: ex.Message);
        }

        private static SmsFailureReason? ClassifyMessage(string? message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return null;

            var m = message.ToLowerInvariant();

            if (m.Contains("اعتبار") || m.Contains("credit") || m.Contains("موجودی"))
                return SmsFailureReason.InsufficientCredit;
            if (m.Contains("قالب") || m.Contains("template"))
                return SmsFailureReason.InvalidTemplate;
            if (m.Contains("پارامتر") || m.Contains("parameter"))
                return SmsFailureReason.InvalidParameter;
            if (m.Contains("خط") || m.Contains("line"))
                return SmsFailureReason.InvalidLineNumber;
            if (m.Contains("موبایل") || m.Contains("mobile") || m.Contains("شماره"))
                return SmsFailureReason.InvalidMobile;
            if (m.Contains("کلید") || m.Contains("api key") || m.Contains("unauthor"))
                return SmsFailureReason.Unauthorized;

            return null;
        }

        private static string FriendlyMessage(SmsFailureReason reason) => reason switch
        {
            SmsFailureReason.Unauthorized => "کلید API نامعتبر است.",
            SmsFailureReason.InsufficientCredit => "اعتبار حساب پیامک کافی نیست.",
            SmsFailureReason.AccessDenied => "دسترسی به سرویس پیامک مجاز نیست.",
            SmsFailureReason.TooManyRequests => "تعداد درخواست‌ها بیش از حد مجاز است.",
            SmsFailureReason.Network or SmsFailureReason.Timeout => "ارتباط با سرویس پیامک برقرار نشد.",
            _ => "بررسی وضعیت حساب پیامک ناموفق بود."
        };
    }
}
