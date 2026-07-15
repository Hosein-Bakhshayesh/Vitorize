using Vitorize.Application.DTOs.Admin.System;

namespace Vitorize.Application.Interfaces;

public interface IAdminMonitoringService
{
    Task<AdminMonitoringDto> GetAsync(CancellationToken cancellationToken = default);
}

public interface IWorkerHeartbeatRegistry
{
    void Record(string workerName, int batchCount, TimeSpan duration, string outcome);
    IReadOnlyList<WorkerHeartbeatDto> Snapshot(TimeSpan healthyWithin);
}
