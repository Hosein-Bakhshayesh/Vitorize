using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class AuditService : IAuditService
    {
        private readonly VitorizeDbContext _dbContext;

        public AuditService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task LogAsync(
            Guid? userId,
            string actionType,
            string entityName,
            string? entityId = null,
            string? data = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            await _dbContext.AuditLogs.AddAsync(new AuditLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                ActionType = actionType,
                EntityName = entityName,
                EntityId = entityId,
                Data = data,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }
    }
}