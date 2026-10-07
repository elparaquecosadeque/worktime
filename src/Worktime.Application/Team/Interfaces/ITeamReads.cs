using Worktime.Application.Team.Results;

namespace Worktime.Application.Team.Interfaces;

public interface ITeamReads
{
    /// <param name="supervisorId">Only this supervisor's active workers; null = every active worker.</param>
    Task<IReadOnlyList<TeamWorkerRow>> ListWorkersAsync(Guid? supervisorId, CancellationToken ct);
    Task<IReadOnlyList<TeamSupervisorRow>> ListSupervisorsAsync(CancellationToken ct);
    Task<IReadOnlyList<TeamLogRow>> ListLogsSinceAsync(IReadOnlyCollection<Guid> workerIds, DateTimeOffset fromUtc, CancellationToken ct);
}
