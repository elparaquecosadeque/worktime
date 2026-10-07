using Mediator;
using Worktime.Application.Common;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Application.WorkLogs.Results;

namespace Worktime.Application.WorkLogs.Queries;

public sealed record InboxQuery(Actor Actor, bool OrphansOnly, Guid? WorkerId, DateTimeOffset? From, DateTimeOffset? To) : IQuery<IReadOnlyList<WorkLogDto>>;

/// <summary>Never cached: this list feeds decisions.</summary>
public sealed class InboxHandler(IWorkLogReads reads) : IQueryHandler<InboxQuery, IReadOnlyList<WorkLogDto>>
{
    public async ValueTask<IReadOnlyList<WorkLogDto>> Handle(InboxQuery q, CancellationToken ct)
    {
        if (q.OrphansOnly && !q.Actor.SeesAllTeams) throw new ForbiddenException("worklog.orphans_admin_only");
        var supervisorId = q.Actor.SeesAllTeams ? (Guid?)null : q.Actor.Id;
        return await reads.ListPendingAsync(supervisorId, q.OrphansOnly, q.WorkerId, q.From?.ToUniversalTime(), q.To?.ToUniversalTime(), ct);
    }
}
