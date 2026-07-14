using Microsoft.Extensions.Logging;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms
{
    /// <summary>
    /// سرویس متمرکز پیامک. تنها نقطه‌ای است که برنامه برای ارسال پیامک از آن استفاده می‌کند.
    /// نرمال‌سازی شماره، انتخاب قالب، ارسال، نگاشت خطا و لاگ‌گیری امن (پنهان‌سازی شماره) را انجام می‌دهد.
    /// </summary>
    public sealed class SmsService : ISmsService
    {
        private static readonly HashSet<SmsFailureReason> TransientReasons = new()
        {
            SmsFailureReason.Network,
            SmsFailureReason.Timeout,
            SmsFailureReason.ProviderUnavailable
        };

        private readonly ISmsSettingsProvider _settings;
        private readonly ISmsSender _sender;
        private readonly ILogger<SmsService> _logger;

        public SmsService(
            ISmsSettingsProvider settings,
            ISmsSender sender,
            ILogger<SmsService> logger)
        {
            _settings = settings;
            _sender = sender;
            _logger = logger;
        }

        public bool TryNormalizeMobile(string? input, out string normalized) =>
            IranMobile.TryNormalize(input, out normalized);

        public async Task<bool> IsEnabledAsync(CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);
            return options.IsOperational;
        }

        public async Task<SmsSendResult> SendTemplateAsync(
            string mobile,
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters,
            CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);

            if (!options.IsEnabled)
                return SmsSendResult.Failure(SmsFailureReason.Disabled);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                return SmsSendResult.Failure(SmsFailureReason.NotConfigured);

            if (!TryNormalizeMobile(mobile, out var normalized))
                return SmsSendResult.Failure(SmsFailureReason.InvalidMobile, "شماره موبایل معتبر نیست.");

            var requiredParameters = SmsTemplateContract.GetRequiredParameterNames(templateKey);
            if (requiredParameters is null)
                return SmsSendResult.Failure(SmsFailureReason.InvalidTemplate);

            if (!SmsTemplateContract.HasExactParameters(templateKey, parameters))
                return SmsSendResult.Failure(
                    SmsFailureReason.InvalidParameter,
                    $"Required parameters: {string.Join(", ", requiredParameters)}");

            var templateId = options.GetTemplateId(templateKey);

            if (templateId is null)
            {
                _logger.LogWarning(
                    "SMS template not configured. TemplateKey={TemplateKey} Mobile={Mobile}",
                    templateKey, IranMobile.Mask(normalized));
                return SmsSendResult.Failure(SmsFailureReason.InvalidTemplate);
            }

            var result = await SendWithRetryAsync(
                ct => _sender.SendVerifyAsync(options.ApiKey!, normalized, templateId.Value, parameters, ct),
                options.MaxRetryCount,
                cancellationToken);

            LogResult(result, templateKey, IranMobile.Mask(normalized), templateId, options.Provider);
            return result;
        }

        public async Task<SmsSendResult> SendTextAsync(
            string mobile,
            string text,
            CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);

            if (!options.IsEnabled)
                return SmsSendResult.Failure(SmsFailureReason.Disabled);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                return SmsSendResult.Failure(SmsFailureReason.NotConfigured);

            if (options.DefaultLineNumber is null or <= 0)
                return SmsSendResult.Failure(SmsFailureReason.InvalidLineNumber);

            if (!TryNormalizeMobile(mobile, out var normalized))
                return SmsSendResult.Failure(SmsFailureReason.InvalidMobile, "شماره موبایل معتبر نیست.");

            if (string.IsNullOrWhiteSpace(text))
                return SmsSendResult.Failure(SmsFailureReason.InvalidParameter);

            var result = await SendWithRetryAsync(
                ct => _sender.SendBulkAsync(options.ApiKey!, options.DefaultLineNumber!.Value, text, normalized, ct),
                options.MaxRetryCount,
                cancellationToken);

            LogResult(result, "Text", IranMobile.Mask(normalized), null, options.Provider);
            return result;
        }

        public Task<SmsSendResult> SendCustomTextAsync(
            string mobile, string text, CancellationToken cancellationToken = default) =>
            SendTextAsync(mobile, text, cancellationToken);

        public Task<SmsSendResult> SendOtpAsync(
            string mobile,
            string templateKey,
            string code,
            int expiryMinutes,
            CancellationToken cancellationToken = default)
        {
            var parameters = new[]
            {
                new SmsTemplateParameter(SmsTemplateParams.Code, code),
                new SmsTemplateParameter(SmsTemplateParams.Expire, expiryMinutes.ToString())
            };

            return SendTemplateAsync(mobile, templateKey, parameters, cancellationToken);
        }

        public Task<SmsSendResult> SendLoginOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken ct = default) =>
            SendOtpAsync(mobile, SmsTemplateKeys.LoginOtp, code, expiryMinutes, ct);

        public Task<SmsSendResult> SendRegisterOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken ct = default) =>
            SendOtpAsync(mobile, SmsTemplateKeys.RegisterOtp, code, expiryMinutes, ct);

        public Task<SmsSendResult> SendForgotPasswordOtpAsync(string mobile, string code, int expiryMinutes, CancellationToken ct = default) =>
            SendOtpAsync(mobile, SmsTemplateKeys.ForgotPassword, code, expiryMinutes, ct);

        public async Task<SmsAccountStatus> GetAccountStatusAsync(CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                return new SmsAccountStatus { IsSuccess = false, UserMessage = "کلید API تنظیم نشده است." };

            return await _sender.GetAccountStatusAsync(options.ApiKey!, cancellationToken);
        }

        public async Task<(bool IsValid, string Message)> ValidateConfigurationAsync(CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);

            if (!options.IsEnabled)
                return (false, "سرویس پیامک غیرفعال است.");

            if (string.IsNullOrWhiteSpace(options.ApiKey))
                return (false, "کلید API پیامک تنظیم نشده است.");

            if (options.GetTemplateId(SmsTemplateKeys.GenericOtp) is null)
                return (false, "شناسه قالب یکپارچه OTP تنظیم نشده است (پارامترها: CODE و EXPIRE). ");

            if (options.GetTemplateId(SmsTemplateKeys.UniversalNotification) is null)
                return (false, "شناسه قالب عمومی اطلاع‌رسانی تنظیم نشده است (پارامتر: ORDER_NUMBER). ");

            return (true, "پیکربندی پیامک معتبر است.");
        }

        public async Task<int?> GetTemplateIdAsync(string templateKey, CancellationToken cancellationToken = default)
        {
            var options = await _settings.GetAsync(cancellationToken);
            return options.GetTemplateId(templateKey);
        }

        private static async Task<SmsSendResult> SendWithRetryAsync(
            Func<CancellationToken, Task<SmsSendResult>> action,
            int maxRetryCount,
            CancellationToken cancellationToken)
        {
            // تلاش هم‌زمان با backoff کوتاه فقط برای خطاهای گذرا؛ ماندگاری واقعی توسط Outbox انجام می‌شود.
            var attempts = Math.Clamp(maxRetryCount, 0, 3) + 1;
            SmsSendResult result = SmsSendResult.Failure(SmsFailureReason.Unknown);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                result = await action(cancellationToken);

                if (result.IsSuccess || !TransientReasons.Contains(result.FailureReason))
                    return result;

                if (attempt < attempts)
                    await Task.Delay(TimeSpan.FromMilliseconds(400 * attempt), cancellationToken);
            }

            return result;
        }

        private void LogResult(
            SmsSendResult result,
            string purpose,
            string maskedMobile,
            int? templateId,
            string provider)
        {
            if (result.IsSuccess)
            {
                _logger.LogInformation(
                    "SMS sent. Purpose={Purpose} Mobile={Mobile} Provider={Provider} TemplateId={TemplateId} MessageId={MessageId} Cost={Cost}",
                    purpose, maskedMobile, provider, templateId, result.ProviderMessageId, result.Cost);
            }
            else
            {
                _logger.LogWarning(
                    "SMS failed. Purpose={Purpose} Mobile={Mobile} Provider={Provider} TemplateId={TemplateId} Reason={Reason} ProviderStatus={Status} ProviderMessage={ProviderMessage}",
                    purpose, maskedMobile, provider, templateId, result.FailureReason, result.ProviderStatus, result.ProviderMessage);
            }
        }
    }
}
