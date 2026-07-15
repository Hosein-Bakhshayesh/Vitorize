using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Outbox;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services.Sms
{
    /// <summary>
    /// ثبت پیامک رویدادهای تجاری در Outbox. عمداً SaveChanges صدا نمی‌زند تا در همان تراکنش
    /// فراخواننده commit شود؛ در نتیجه شکست پیامک هرگز عملیات تجاری را برنمی‌گرداند.
    /// idempotency بر اساس (نوع پیام + شناسه موجودیت + رویداد) تضمین می‌شود.
    /// </summary>
    public sealed class SmsOutboxEnqueuer : ISmsOutboxEnqueuer
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ISmsSettingsProvider _settingsProvider;

        public SmsOutboxEnqueuer(
            VitorizeDbContext dbContext,
            ISmsSettingsProvider settingsProvider)
        {
            _dbContext = dbContext;
            _settingsProvider = settingsProvider;
        }

        public async Task EnqueueTemplateAsync(
            string? mobile,
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters,
            string purpose,
            Guid? aggregateId,
            CancellationToken cancellationToken = default,
            Guid? userId = null,
            Guid? createdByUserId = null,
            string? relatedEntityType = null,
            string? relatedEntityReference = null,
            string? idempotencyKey = null,
            string? internalNote = null)
        {
            // بدون شماره معتبر، چیزی برای ارسال نیست.
            if (string.IsNullOrWhiteSpace(mobile) || !IranMobile.TryNormalize(mobile, out var normalized))
                return;

            // اعلان سفارشی مدیر تنها استثناست؛ سایر رویدادهای خودکار باید صریحاً در
            // سیاست مرکزی مجاز شده باشند. این محافظ از بازگشت ناخواسته‌ی رویدادهای
            // حذف‌شده مانند OrderCreated و WalletTransaction جلوگیری می‌کند.
            var isAdminNotification =
                templateKey.Equals(SmsTemplateKeys.UniversalNotification, StringComparison.OrdinalIgnoreCase) &&
                purpose.Equals("AdminCustomNotification", StringComparison.OrdinalIgnoreCase);
            if (!isAdminNotification && !SmsAutomaticEventPolicy.IsAllowedTemplate(templateKey))
                return;

            // جلوگیری از پیامک تکراری برای یک رویداد مشخص.
            if (aggregateId.HasValue)
            {
                var exists = await _dbContext.OutboxMessages.AnyAsync(
                    x => x.MessageType == OutboxMessageTypes.SmsSend &&
                         x.AggregateId == aggregateId &&
                         x.AggregateType == purpose,
                    cancellationToken);

                if (exists)
                    return;
            }

            var outboxId = Guid.NewGuid();
            var historyId = Guid.NewGuid();
            var publicReference = parameters
                .FirstOrDefault(x => x.Name == SmsTemplateParams.OrderNumber)?.Value;
            var options = await _settingsProvider.GetAsync(cancellationToken);
            var finalIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                ? $"sms:{purpose}:{aggregateId?.ToString("N") ?? outboxId.ToString("N")}"
                : idempotencyKey.Trim();

            if (await _dbContext.SmsMessages.AnyAsync(
                    x => x.IdempotencyKey == finalIdempotencyKey, cancellationToken))
                return;

            var payloadCorrelationId = ResolvePayloadCorrelationId();
            var payload = new SmsOutboxPayload
            {
                SmsMessageId = historyId,
                CorrelationId = payloadCorrelationId,
                Mobile = normalized,
                TemplateKey = templateKey,
                Purpose = purpose,
                Parameters = parameters
                    .Select(p => new SmsOutboxParameter { Name = p.Name, Value = p.Value })
                    .ToList()
            };

            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = outboxId,
                MessageType = OutboxMessageTypes.SmsSend,
                AggregateId = aggregateId,
                AggregateType = purpose,
                Payload = JsonSerializer.Serialize(payload),
                Status = (byte)OutboxMessageStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _dbContext.SmsMessages.AddAsync(new SmsMessage
            {
                Id = historyId,
                UserId = userId,
                Mobile = normalized,
                MaskedMobile = IranMobile.Mask(normalized),
                Purpose = purpose,
                SendType = (byte)(SmsTemplateKeys.IsOtp(templateKey)
                    ? SmsSendType.OtpTemplate
                    : SmsSendType.NotificationTemplate),
                TemplateKey = templateKey,
                TemplateId = options.GetTemplateId(templateKey),
                PublicReference = publicReference,
                SafeMessagePreview = SmsTemplateKeys.IsOtp(templateKey)
                    ? "قالب امن کد یکبار مصرف"
                    : publicReference is null ? null : $"اعلان با کد پیگیری {publicReference}",
                InternalNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim(),
                Provider = options.Provider,
                Status = (byte)SmsMessageStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = 5,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = aggregateId,
                RelatedEntityReference = relatedEntityReference ?? publicReference,
                IdempotencyKey = finalIdempotencyKey,
                CorrelationId = ResolveCorrelationId(payloadCorrelationId),
                OutboxMessageId = outboxId
            }, cancellationToken);
        }

        public async Task EnqueueTextAsync(
            string? mobile,
            string text,
            string purpose,
            Guid? aggregateId,
            CancellationToken cancellationToken = default,
            Guid? userId = null,
            Guid? createdByUserId = null,
            string? relatedEntityType = null,
            string? relatedEntityReference = null,
            string? idempotencyKey = null,
            string? internalNote = null)
        {
            if (string.IsNullOrWhiteSpace(mobile) ||
                !IranMobile.TryNormalize(mobile, out var normalized) ||
                string.IsNullOrWhiteSpace(text))
                return;

            var outboxId = Guid.NewGuid();
            var historyId = Guid.NewGuid();
            var finalIdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey)
                ? $"sms:{purpose}:{aggregateId?.ToString("N") ?? outboxId.ToString("N")}"
                : idempotencyKey.Trim();

            if (await _dbContext.SmsMessages.AnyAsync(
                    x => x.IdempotencyKey == finalIdempotencyKey, cancellationToken))
                return;

            var options = await _settingsProvider.GetAsync(cancellationToken);
            var payloadCorrelationId = ResolvePayloadCorrelationId();
            var payload = new SmsOutboxPayload
            {
                SmsMessageId = historyId,
                CorrelationId = payloadCorrelationId,
                Mobile = normalized,
                Text = text.Trim(),
                Purpose = purpose
            };

            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = outboxId,
                MessageType = OutboxMessageTypes.SmsSend,
                AggregateId = aggregateId,
                AggregateType = purpose,
                Payload = JsonSerializer.Serialize(payload),
                Status = (byte)OutboxMessageStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);

            await _dbContext.SmsMessages.AddAsync(new SmsMessage
            {
                Id = historyId,
                UserId = userId,
                Mobile = normalized,
                MaskedMobile = IranMobile.Mask(normalized),
                Purpose = purpose,
                SendType = (byte)SmsSendType.CustomText,
                SafeMessagePreview = text.Trim(),
                InternalNote = string.IsNullOrWhiteSpace(internalNote) ? null : internalNote.Trim(),
                Provider = options.Provider,
                Status = (byte)SmsMessageStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = 5,
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = createdByUserId,
                RelatedEntityType = relatedEntityType,
                RelatedEntityId = aggregateId,
                RelatedEntityReference = relatedEntityReference,
                IdempotencyKey = finalIdempotencyKey,
                CorrelationId = ResolveCorrelationId(payloadCorrelationId),
                OutboxMessageId = outboxId
            }, cancellationToken);
        }

        private static Guid ResolveCorrelationId(string correlation) =>
            Guid.TryParseExact(correlation, "N", out var correlationId)
                ? correlationId
                : Guid.NewGuid();

        private static string ResolvePayloadCorrelationId() =>
            CorrelationIdPolicy.IsValid(CorrelationContext.Current)
                ? CorrelationContext.Current!
                : CorrelationIdPolicy.Generate();
    }
}
