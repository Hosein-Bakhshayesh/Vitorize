using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class SecurityLogService : ISecurityLogService
    {
        private readonly VitorizeDbContext _dbContext;

        public SecurityLogService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task LogAsync(
            Guid? userId,
            string eventType,
            bool isSuccessful,
            string? description = null,
            string? ipAddress = null,
            string? userAgent = null)
        {
            await _dbContext.SecurityLogs.AddAsync(new SecurityLog
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                EventType = eventType,
                Description = description,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccessful = isSuccessful,
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync();
        }
    }
}