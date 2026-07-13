using Vitorize.Application.Models.Sms;

namespace Vitorize.Application.Interfaces
{
    /// <summary>
    /// ثبت پیامک رویدادهای تجاری در Outbox (بدون فراخوانی SaveChanges؛ تراکنش فراخواننده آن را commit می‌کند).
    /// دارای محافظت idempotency بر اساس (نوع رویداد + شناسه موجودیت) است تا پیامک تکراری ارسال نشود.
    /// </summary>
    public interface ISmsOutboxEnqueuer
    {
        Task EnqueueTemplateAsync(
            string? mobile,
            string templateKey,
            IReadOnlyList<SmsTemplateParameter> parameters,
            string purpose,
            Guid? aggregateId,
            CancellationToken cancellationToken = default);
    }
}
