using System.Collections.Concurrent;
using Vitorize.Application.DTOs.Admin.System;
using Vitorize.Application.Interfaces;
using Vitorize.Shared.Logging;

namespace Vitorize.Infrastructure.Services;

public sealed class WorkerHeartbeatRegistry : IWorkerHeartbeatRegistry
{
    private readonly ConcurrentDictionary<string, State> _workers = new(StringComparer.Ordinal);

    public void Record(string workerName, int batchCount, TimeSpan duration, string outcome)
    {
        var safeName = SensitiveLogData.Sanitize(workerName, 80);
        var safeOutcome = SensitiveLogData.Sanitize(outcome, 80);
        _workers[safeName] = new State(DateTime.UtcNow, Math.Max(0, batchCount), Math.Max(0, (long)duration.TotalMilliseconds), safeOutcome);
    }

    public IReadOnlyList<WorkerHeartbeatDto> Snapshot(TimeSpan healthyWithin)
    {
        var cutoff = DateTime.UtcNow.Subtract(healthyWithin);
        return _workers.OrderBy(x => x.Key).Select(x => new WorkerHeartbeatDto
        {
            WorkerName = x.Key,
            LastHeartbeatUtc = x.Value.At,
            IsHealthy = x.Value.At >= cutoff && !x.Value.Outcome.Equals("Failed", StringComparison.OrdinalIgnoreCase),
            LastBatchCount = x.Value.BatchCount,
            LastDurationMs = x.Value.DurationMs,
            LastOutcome = x.Value.Outcome
        }).ToList();
    }

    private sealed record State(DateTime At, int BatchCount, long DurationMs, string Outcome);
}
