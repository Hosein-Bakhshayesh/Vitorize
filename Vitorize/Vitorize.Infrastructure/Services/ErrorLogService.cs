using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;

namespace Vitorize.Infrastructure.Services
{
    public class ErrorLogService : IErrorLogService
    {
        private readonly VitorizeDbContext _dbContext;

        public ErrorLogService(VitorizeDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task LogAsync(Exception exception)
        {
            try
            {
                await _dbContext.ErrorLogs.AddAsync(new ErrorLog
                {
                    Id = Guid.NewGuid(),
                    Message = exception.Message,
                    StackTrace = exception.StackTrace,
                    Source = exception.Source,
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
            }
            catch
            {
            }
        }
    }
}