using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services.Sms
{
    public sealed class SmsHistoryService : ISmsHistoryService
    {
        private readonly VitorizeDbContext _db;
        private readonly ISmsSettingsProvider _settings;

        public SmsHistoryService(VitorizeDbContext db, ISmsSettingsProvider settings)
        {
            _db = db;
            _settings = settings;
        }

        public async Task<Guid> CreatePendingAsync(
            SmsHistoryRecordRequest request,
            CancellationToken cancellationToken = default)
        {
            var message = await CreateEntityAsync(request, cancellationToken);
            await _db.SmsMessages.AddAsync(message, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return message.Id;
        }

        public async Task<Guid> RecordDirectResultAsync(
            SmsHistoryRecordRequest request,
            SmsSendResult result,
            CancellationToken cancellationToken = default)
        {
            var message = await CreateEntityAsync(request, cancellationToken);
            var now = DateTime.UtcNow;
            message.LastAttemptAt = now;
            message.ProviderMessageId = result.ProviderMessageId;
            message.ProviderErrorCode = result.IsSuccess ? null : result.FailureReason.ToString();
            message.ProviderErrorMessage = result.IsSuccess ? null : result.UserMessage;
            message.DeliveryCost = result.Cost;
            message.Status = result.IsSuccess
                ? (byte)SmsMessageStatus.Sent
                : result.FailureReason == SmsFailureReason.Disabled
                    ? (byte)SmsMessageStatus.Disabled
                    : (byte)SmsMessageStatus.Failed;
            message.SentAt = result.IsSuccess ? now : null;
            message.FailedAt = result.IsSuccess ? null : now;

            message.Attempts.Add(new SmsMessageAttempt
            {
                Id = Guid.NewGuid(),
                AttemptNumber = 1,
                Status = message.Status,
                ProviderMessageId = result.ProviderMessageId,
                ProviderErrorCode = message.ProviderErrorCode,
                ProviderErrorMessage = message.ProviderErrorMessage,
                DeliveryCost = result.Cost,
                AttemptedAt = now,
                CompletedAt = now
            });

            await _db.SmsMessages.AddAsync(message, cancellationToken);
            await _db.SaveChangesAsync(cancellationToken);
            return message.Id;
        }

        private async Task<SmsMessage> CreateEntityAsync(
            SmsHistoryRecordRequest request,
            CancellationToken cancellationToken)
        {
            var options = await _settings.GetAsync(cancellationToken);
            var normalized = IranMobile.TryNormalize(request.Mobile, out var mobile)
                ? mobile
                : request.Mobile.Trim();

            return new SmsMessage
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                Mobile = normalized,
                MaskedMobile = IranMobile.Mask(normalized),
                Purpose = request.Purpose,
                SendType = request.SendType,
                TemplateKey = request.TemplateKey,
                TemplateId = request.TemplateId,
                PublicReference = request.PublicReference,
                SafeMessagePreview = request.SafeMessagePreview,
                InternalNote = request.InternalNote,
                Provider = options.Provider,
                Status = (byte)SmsMessageStatus.Pending,
                RetryCount = 0,
                MaxRetryCount = Math.Clamp(request.MaxRetryCount, 1, 10),
                CreatedAt = DateTime.UtcNow,
                CreatedByUserId = request.CreatedByUserId,
                RelatedEntityType = request.RelatedEntityType,
                RelatedEntityId = request.RelatedEntityId,
                RelatedEntityReference = request.RelatedEntityReference,
                IdempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
                    ? $"sms:{Guid.NewGuid():N}"
                    : request.IdempotencyKey.Trim(),
                CorrelationId = Guid.NewGuid(),
                OutboxMessageId = request.OutboxMessageId
            };
        }
    }
}
