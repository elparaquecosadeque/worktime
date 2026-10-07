using Worktime.Application.WorkLogs.Results;

namespace Worktime.Application.WorkLogs.Interfaces;

public interface IWorkLogReads
{
    Task<IReadOnlyList<WorkLogDto>> ListForWorkerAsync(Guid workerId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct);

    /// <param name="supervisorId">Pending logs of this supervisor's current workers; null = every worker.</param>
    /// <param name="orphansOnly">Only workers without a supervisor (admin inbox).</param>
    Task<IReadOnlyList<WorkLogDto>> ListPendingAsync(Guid? supervisorId, bool orphansOnly, Guid? workerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct);
}
