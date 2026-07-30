using Microsoft.EntityFrameworkCore;
using Vitorize.Infrastructure.Persistence;
using Vitorize.Shared.Logging;

namespace Vitorize.Api.Services;

/// <summary>
/// Verifies dependencies required to accept store traffic. This deliberately has no
/// mutable state: concurrent probes use their request-scoped DbContext and cannot
/// interfere with customer requests or one another.
/// </summary>
public interface IReadinessProbe
{
    Task<bool> IsReadyAsync(CancellationToken cancellationToken = default);
}

public sealed class SqlServerReadinessProbe : IReadinessProbe
{
    internal static readonly TimeSpan MaximumProbeDuration = TimeSpan.FromSeconds(5);

    private readonly VitorizeDbContext _dbContext;
    private readonly ILogger<SqlServerReadinessProbe> _logger;

    public SqlServerReadinessProbe(
        VitorizeDbContext dbContext,
        ILogger<SqlServerReadinessProbe> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<bool> IsReadyAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(MaximumProbeDuration);

        try
        {
            return await _dbContext.Database.CanConnectAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A caller that has gone away must not be turned into a misleading 503.
            throw;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Readiness probe exceeded {TimeoutSeconds} seconds. EventType={EventType}",
                MaximumProbeDuration.TotalSeconds,
                OperationalEventNames.ReadinessProbeTimedOut);
            return false;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                "Readiness probe could not connect to the database. ExceptionType={ExceptionType} EventType={EventType}",
                exception.GetType().Name,
                OperationalEventNames.ReadinessProbeFailed);
            return false;
        }
    }
}
