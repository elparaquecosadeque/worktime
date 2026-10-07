using Mediator;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Team.Results;

namespace Worktime.Application.Team.Queries;

/// <summary>Active supervisors, for pickers (a worker suggesting one, an admin assigning).</summary>
public sealed record ListSupervisorsQuery : IQuery<IReadOnlyList<TeamSupervisorRow>>;

public sealed class ListSupervisorsHandler(ITeamReads reads) : IQueryHandler<ListSupervisorsQuery, IReadOnlyList<TeamSupervisorRow>>
{
    public async ValueTask<IReadOnlyList<TeamSupervisorRow>> Handle(ListSupervisorsQuery q, CancellationToken ct) =>
        await reads.ListSupervisorsAsync(ct);
}
