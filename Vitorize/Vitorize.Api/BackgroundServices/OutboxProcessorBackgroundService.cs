using Microsoft.EntityFrameworkCore;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Api.BackgroundServices
{
    public class OutboxProcessorBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<OutboxProcessorBackgroundService> _logger;

        public OutboxProcessorBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<OutboxProcessorBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Outbox processor failed.");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        private async Task ProcessAsync(CancellationToken cancellationToken)
        {
            using var scope = _scopeFactory.CreateScope();

            var dbContext = scope.ServiceProvider
                .GetRequiredService<VitorizeDbContext>();

            var messages = await dbContext.OutboxMessages
                .Where(x =>
                    x.Status == (byte)OutboxMessageStatus.Pending ||
                    x.Status == (byte)OutboxMessageStatus.Failed &&
                    x.RetryCount < 5)
                .OrderBy(x => x.CreatedAt)
                .Take(20)
                .ToListAsync(cancellationToken);

            foreach (var message in messages)
            {
                try
                {
                    message.Status = (byte)OutboxMessageStatus.Processing;
                    await dbContext.SaveChangesAsync(cancellationToken);

                    // فعلاً فقط لاگ می‌کنیم.
                    // بعداً اینجا ارسال SMS / Email / Push / Webhook اضافه می‌شود.
                    _logger.LogInformation(
                        "Processing Outbox Message: {MessageType} - {Payload}",
                        message.MessageType,
                        message.Payload);

                    message.Status = (byte)OutboxMessageStatus.Processed;
                    message.ProcessedAt = DateTime.UtcNow;
                    message.ErrorMessage = null;

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    message.RetryCount += 1;
                    message.Status = message.RetryCount >= 5
                        ? (byte)OutboxMessageStatus.Failed
                        : (byte)OutboxMessageStatus.Pending;

                    message.ErrorMessage = ex.Message;

                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}