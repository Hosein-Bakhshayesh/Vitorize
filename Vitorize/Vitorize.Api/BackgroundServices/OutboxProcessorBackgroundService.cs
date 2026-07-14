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
                // ادعای اتمیک رکورد مانع ارسال تکراری در استقرار چند نمونه‌ای API می‌شود.
                var claimed = await dbContext.OutboxMessages
                    .Where(x => x.Id == message.Id && x.Status == (byte)OutboxMessageStatus.Pending)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(x => x.Status, (byte)OutboxMessageStatus.Processing),
                        cancellationToken);
                if (claimed == 0)
                    return;

                dbContext.Entry(message).State = EntityState.Detached;
                message = await dbContext.OutboxMessages
                    .SingleAsync(x => x.Id == message.Id, cancellationToken);

                var handled = await HandleAsync(dbContext, smsService, message, cancellationToken);

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
                _logger.LogError(ex, "Outbox SMS processing failed. OutboxId={OutboxId}", message.Id);
                message.RetryCount += 1;
                message.Status = message.RetryCount >= MaxRetryCount
                    ? (byte)OutboxMessageStatus.Failed
                    : (byte)OutboxMessageStatus.Pending;
                message.ProcessedAt = DateTime.UtcNow;
                message.ErrorMessage = Truncate(ex.Message, 2000);

                var history = await dbContext.SmsMessages
                    .Include(x => x.Attempts)
                    .FirstOrDefaultAsync(x => x.OutboxMessageId == message.Id, cancellationToken);
                if (history is not null)
                {
                    var terminal = message.RetryCount >= MaxRetryCount;
                    history.Status = (byte)(terminal
                        ? SmsMessageStatus.DeadLetter
                        : SmsMessageStatus.Retrying);
                    history.RetryCount = message.RetryCount;
                    history.FailedAt = DateTime.UtcNow;
                    history.NextRetryAt = terminal
                        ? null
                        : DateTime.UtcNow.AddSeconds(Math.Min(600, 30 * Math.Pow(2, message.RetryCount - 1)));
                    history.ProviderErrorCode = SmsFailureReason.Unknown.ToString();
                    history.ProviderErrorMessage = "خطای داخلی هنگام پردازش پیامک رخ داد.";

                    var attempt = history.Attempts
                        .OrderByDescending(x => x.AttemptNumber)
                        .FirstOrDefault(x => x.CompletedAt == null);
                    if (attempt is not null)
                    {
                        attempt.Status = history.Status;
                        attempt.ProviderErrorCode = history.ProviderErrorCode;
                        attempt.ProviderErrorMessage = history.ProviderErrorMessage;
                        attempt.CompletedAt = DateTime.UtcNow;
                    }
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<HandleResult> HandleAsync(
            VitorizeDbContext dbContext,
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

                var history = payload.SmsMessageId.HasValue
                    ? await dbContext.SmsMessages
                        .Include(x => x.Attempts)
                        .FirstOrDefaultAsync(x => x.Id == payload.SmsMessageId.Value, cancellationToken)
                    : await dbContext.SmsMessages
                        .Include(x => x.Attempts)
                        .FirstOrDefaultAsync(x => x.OutboxMessageId == message.Id, cancellationToken);

                var attemptedAt = DateTime.UtcNow;
                Vitorize.Domain.Entities.SmsMessageAttempt? attempt = null;
                if (history is not null)
                {
                    history.Status = (byte)SmsMessageStatus.Processing;
                    history.LastAttemptAt = attemptedAt;
                    history.NextRetryAt = null;
                    attempt = new Vitorize.Domain.Entities.SmsMessageAttempt
                    {
                        Id = Guid.NewGuid(),
                        SmsMessageId = history.Id,
                        AttemptNumber = history.Attempts.Count + 1,
                        Status = (byte)SmsMessageStatus.Processing,
                        AttemptedAt = attemptedAt
                    };
                    history.Attempts.Add(attempt);
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                SmsSendResult result;
                if (!string.IsNullOrWhiteSpace(payload.TemplateKey))
                {
                    IReadOnlyList<SmsTemplateParameter> parameters = (payload.Parameters ?? new List<SmsOutboxParameter>())
                        .Select(p => new SmsTemplateParameter(p.Name, p.Value))
                        .ToList();

                    // سازگاری صف‌های قبلی: استخراج/نگاشت ORDER_NUMBER و کنارگذاشتن فیلدهای قدیمی.
                    parameters = SmsTemplateContract.NormalizeQueuedParameters(
                        payload.TemplateKey!, parameters);

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

                if (history is not null && attempt is not null)
                {
                    var completedAt = DateTime.UtcNow;
                    var retryableFailure = !result.IsSuccess && SmsRetryPolicy.IsRetryable(result.FailureReason);
                    var reachesLimit = message.RetryCount + 1 >= MaxRetryCount;
                    var finalStatus = result.IsSuccess
                        ? SmsMessageStatus.Sent
                        : result.FailureReason == SmsFailureReason.Disabled
                            ? SmsMessageStatus.Disabled
                            : retryableFailure && !reachesLimit
                                ? SmsMessageStatus.Retrying
                                : reachesLimit
                                    ? SmsMessageStatus.DeadLetter
                                    : SmsMessageStatus.Failed;

                    history.Status = (byte)finalStatus;
                    history.RetryCount = message.RetryCount + (result.IsSuccess ? 0 : 1);
                    history.ProviderMessageId = result.ProviderMessageId;
                    history.ProviderErrorCode = result.IsSuccess ? null : result.FailureReason.ToString();
                    history.ProviderErrorMessage = result.IsSuccess ? null : result.UserMessage;
                    history.DeliveryCost = result.Cost;
                    history.SentAt = result.IsSuccess ? completedAt : null;
                    history.FailedAt = result.IsSuccess ? null : completedAt;
                    history.NextRetryAt = finalStatus == SmsMessageStatus.Retrying
                        ? completedAt.AddSeconds(Math.Min(600, 30 * Math.Pow(2, message.RetryCount)))
                        : null;

                    attempt.Status = (byte)finalStatus;
                    attempt.ProviderMessageId = result.ProviderMessageId;
                    attempt.ProviderErrorCode = history.ProviderErrorCode;
                    attempt.ProviderErrorMessage = history.ProviderErrorMessage;
                    attempt.DeliveryCost = result.Cost;
                    attempt.CompletedAt = completedAt;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                if (result.IsSuccess)
                {
                    _logger.LogInformation(
                        "Outbox SMS sent. Purpose={Purpose} MessageId={MessageId}",
                        payload.Purpose, result.ProviderMessageId);
                    return HandleResult.Ok();
                }

                // پیکربندی غیرفعال/ناقص → قابل بازتلاش نیست (تا اصلاح تنظیمات معنا ندارد بازتلاش شود).
                var retryable = SmsRetryPolicy.IsRetryable(result.FailureReason);

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
