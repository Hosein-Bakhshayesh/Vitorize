using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Enums;

namespace Vitorize.Infrastructure.Services
{
    public class OutboxService : IOutboxService
    {
        private readonly VitorizeDbContext _dbContext;

        public OutboxService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task AddAsync(
            string messageType,
            string payload,
            Guid? aggregateId = null,
            string? aggregateType = null)
        {
            await _dbContext.OutboxMessages.AddAsync(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                AggregateId = aggregateId,
                AggregateType = aggregateType,
                MessageType = messageType,
                Payload = payload,
                Status = (byte)OutboxMessageStatus.Pending,
                RetryCount = 0,
                CreatedAt = DateTime.UtcNow
            });
        }
    }
}