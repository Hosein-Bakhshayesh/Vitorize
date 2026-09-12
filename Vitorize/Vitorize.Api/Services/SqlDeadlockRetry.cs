using Microsoft.Data.SqlClient;
using Vitorize.Shared.Exceptions;

namespace Vitorize.Api.Services;

/// <summary>
/// Retries an idempotent request once after SQL Server selects it as a deadlock victim.
/// The retry receives a fresh DI scope so the failed DbContext and transaction are never reused.
/// </summary>
internal static class SqlDeadlockRetry
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(150);

    public static async Task<T> ExecuteOnceAsync<T>(
        Func<Task<T>> operation,
        Func<Task<T>> retryWithFreshScope,
        ILogger logger,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await operation();
        }
        catch (Exception exception) when (IsDeadlock(exception))
        {
            logger.LogWarning(exception,
                "SQL deadlock occurred during {Operation}. Retrying once with a fresh scope.",
                operationName);

            await Task.Delay(RetryDelay, cancellationToken);
            try
            {
                return await retryWithFreshScope();
            }
            catch (Exception retryException) when (IsDeadlock(retryException))
            {
                logger.LogWarning(retryException,
                    "SQL deadlock occurred again during {Operation} after retry.", operationName);
                throw new BusinessException("سامانه در حال پردازش هم‌زمان درخواست شماست؛ لطفاً چند لحظه دیگر دوباره تلاش کنید.");
            }
        }
    }

    private static bool IsDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 1205 })
                return true;
        }

        return false;
    }
}
