using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Vitorize.Application.Common;
using Vitorize.Application.DTOs.Outbox;
using Vitorize.Application.Interfaces;
using Vitorize.Application.Models.Sms;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

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

        public SmsOutboxEnqueuer(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task EnqueueTemplateAsync(
            string? mobile,
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters,
            string purpose,
            Guid? aggregateId,
            CancellationToken cancellationToken = default)
        {
            // بدون شماره معتبر، چیزی برای ارسال نیست.
            if (string.IsNullOrWhiteSpace(mobile) || !IranMobile.TryNormalize(mobile, out var normalized))
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

            var payload = new SmsOutboxPayload
            {
                Mobile = normalized,
                TemplateKey = templateKey,
                Purpose = purpose,
                Parameters = parameters
                    .Select(p => new SmsOutboxParameter { Name = p.Name, Value = p.Value })
                    .ToList()
            };

            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.SmsSend,
                AggregateId = aggregateId,
                AggregateType = purpose,
                Payload = JsonSerializer.Serialize(payload),
                Status = (byte)OutboxMessageStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }
    }
}
