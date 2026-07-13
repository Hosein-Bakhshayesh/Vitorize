using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Outbox;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Api.BackgroundServices
{
    /// <summary>
    /// پردازشگر Outbox. پیام‌های «SmsSend» را از طریق سرویس متمرکز پیامک ارسال می‌کند و
    /// در صورت خطای گذرا با backoff نمایی و سقف تلاش، بازتلاش/dead-letter انجام می‌دهد.
    /// شکست ارائه‌دهنده هرگز روی تراکنش تجاری اثری ندارد چون این‌جا پس از commit اجرا می‌شود.
    /// </summary>
    public class OutboxProcessorBackgroundService : BackgroundService
    {
        private const int MaxRetryCount = 5;
        private const int BatchSize = 20;
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(10);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessorBackgroundService> _logger;

        public OutboxProcessorBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxProcessorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processor failed.");
                }

                try
                {
                    await Task.Delay(PollInterval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<VitorizeDbContext>();
            var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();

            var now = DateTime.UtcNow;

            // نامزدها: Pending که زمان تلاش بعدی‌شان رسیده باشد (backoff از طریق ProcessedAt = آخرین تلاش).
            var candidates = await dbContext.OutboxMessages
                .Where(x =>
                    x.Status == (byte)OutboxMessageStatus.Pending &&
                    x.RetryCount < MaxRetryCount)
                .OrderBy(x => x.CreatedAt)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var message in candidates)
            {
                if (!IsDue(message, now))
                    continue;

                await ProcessOneAsync(dbContext, smsService, message, cancellationToken);
            }
        }

        private async Task ProcessOneAsync(
            VitorizeDbContext dbContext,
            ISmsService smsService,
            Vitorize.Domain.Entities.OutboxMessage message,
            CancellationToken cancellationToken)
        {
            try
            {
                message.Status = (byte)OutboxMessageStatus.Processing;
                await dbContext.SaveChangesAsync(cancellationToken);

                var handled = await HandleAsync(smsService, message, cancellationToken);

                if (handled.Success)
                {
                    message.Status = (byte)OutboxMessageStatus.Processed;
                    message.ProcessedAt = DateTime.UtcNow;
                    message.ErrorMessage = null;
                }
                else if (handled.Retryable && message.RetryCount + 1 < MaxRetryCount)
                {
                    message.RetryCount += 1;
                    message.Status = (byte)OutboxMessageStatus.Pending;
                    message.ProcessedAt = DateTime.UtcNow; // آخرین زمان تلاش، مبنای backoff
                    message.ErrorMessage = Truncate(handled.Error, 2000);
                }
                else
                {
                    // خطای دائمی یا اتمام سقف تلاش → dead-letter.
                    message.RetryCount += 1;
                    message.Status = (byte)OutboxMessageStatus.Failed;
                    message.ProcessedAt = DateTime.UtcNow;
                    message.ErrorMessage = Truncate(handled.Error, 2000);
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                message.RetryCount += 1;
                message.Status = message.RetryCount >= MaxRetryCount
                    ? (byte)OutboxMessageStatus.Failed
                    : (byte)OutboxMessageStatus.Pending;
                message.ProcessedAt = DateTime.UtcNow;
                message.ErrorMessage = Truncate(ex.Message, 2000);

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<HandleResult> HandleAsync(
            ISmsService smsService,
            Vitorize.Domain.Entities.OutboxMessage message,
            CancellationToken cancellationToken)
        {
            if (message.MessageType == OutboxMessageTypes.SmsSend)
            {
                SmsOutboxPayload? payload;
                try
                {
                    payload = JsonSerializer.Deserialize<SmsOutboxPayload>(message.Payload);
                }
                catch (Exception ex)
                {
                    // پیام مسموم (poison): قابل بازتلاش نیست.
                    return HandleResult.Fail($"Invalid SMS payload: {ex.Message}", retryable: false);
                }

                if (payload is null || string.IsNullOrWhiteSpace(payload.Mobile))
                    return HandleResult.Fail("Empty SMS payload.", retryable: false);

                SmsSendResult result;
                if (!string.IsNullOrWhiteSpace(payload.TemplateKey))
                {
                    var parameters = payload.Parameters
                        .Select(p => new SmsTemplateParameter(p.Name, p.Value))
                        .ToList();

                    result = await smsService.SendTemplateAsync(
                        payload.Mobile, payload.TemplateKey!, parameters, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(payload.Text))
                {
                    result = await smsService.SendTextAsync(payload.Mobile, payload.Text!, cancellationToken);
                }
                else
                {
                    return HandleResult.Fail("SMS payload has neither template nor text.", retryable: false);
                }

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Outbox SMS sent. Purpose={Purpose} MessageId={MessageId}",
                        payload.Purpose, result.ProviderMessageId);
                    return HandleResult.Ok();
                }

                // پیکربندی غیرفعال/ناقص → قابل بازتلاش نیست (تا اصلاح تنظیمات معنا ندارد بازتلاش شود).
                var retryable = result.FailureReason is not (
                    SmsFailureReason.Disabled or
                    SmsFailureReason.NotConfigured or
                    SmsFailureReason.InvalidTemplate or
                    SmsFailureReason.InvalidMobile or
                    SmsFailureReason.InvalidParameter);

                return HandleResult.Fail(
                    $"{payload.Purpose}: {result.FailureReason} {result.ProviderMessage}", retryable);
            }

            if (message.MessageType == OutboxMessageTypes.NotificationCreated)
            {
                // اعلان درون‌برنامه‌ای قبلاً در DB ساخته شده؛ این پیام صرفاً برای گسترش‌های آینده است.
                _logger.LogDebug("Outbox notification processed: {AggregateId}", message.AggregateId);
                return HandleResult.Ok();
            }

            _logger.LogWarning("Unknown outbox message type: {MessageType}", message.MessageType);
            return HandleResult.Ok(); // نوع ناشناخته را نادیده می‌گیریم تا در صف گیر نکند.
        }

        private static bool IsDue(Vitorize.Domain.Entities.OutboxMessage message, DateTime now)
        {
            // تلاش اول بلافاصله؛ سپس backoff نمایی بر اساس RetryCount و آخرین زمان تلاش (ProcessedAt).
            if (message.RetryCount == 0 || message.ProcessedAt is null)
                return true;

            var delaySeconds = Math.Min(600, 30 * Math.Pow(2, message.RetryCount - 1));
            return message.ProcessedAt.Value.AddSeconds(delaySeconds) <= now;
        }

        private static string? Truncate(string? value, int max) =>
            string.IsNullOrEmpty(value) || value.Length <= max ? value : value.Substring(0, max);

        private readonly record struct HandleResult(bool Success, bool Retryable, string? Error)
        {
            public static HandleResult Ok() => new(true, false, null);
            public static HandleResult Fail(string error, bool retryable) => new(false, retryable, error);
        }
    }
}
