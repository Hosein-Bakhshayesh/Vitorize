using Vitorize.Application.Interfaces;
using Vitorize.Domain.Entities;
using Vitorize.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services
{
    public class ErrorLogService : IErrorLogService
    {
        private readonly VitorizeDbContext _dbContext;
        private readonly ILogger<ErrorLogService> _logger;

        public ErrorLogService(VitorizeDbContext dbContext, ILogger<ErrorLogService> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        public async Task LogAsync(Exception exception)
        {
            try
            {
                await _dbContext.ErrorLogs.AddAsync(new ErrorLog
                {
                    Id = Guid.NewGuid(),
                    Message = SensitiveLogData.SafeExceptionMessage(exception),
                    StackTrace = SensitiveLogData.RedactFreeText(exception.StackTrace, 8000),
                    Source = SensitiveLogData.Sanitize(exception.Source ?? exception.GetType().FullName, 300),
                    CreatedAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
            }
            catch (Exception persistenceException)
            {
                _logger.LogWarning(
                    "ErrorLog persistence failed. ExceptionType={ExceptionType} EventType={EventType}",
                    persistenceException.GetType().Name,
                    "ErrorLogPersistenceFailed");
            }
        }
    }
}
