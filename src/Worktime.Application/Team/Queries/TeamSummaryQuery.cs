using Mediator;
using Microsoft.Extensions.Options;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Team.Results;
using Worktime.Application.WorkLogs.Queries;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Team.Queries;

public sealed record TeamSummaryQuery(Actor Actor) : IQuery<TeamSummaryDto>;

/// <summary>
/// The expensive aggregate every dashboard asks for on load/reconnect. Cached briefly (Cache:TeamSummarySeconds)
/// and never invalidated: live events carry deltas, the cache only absorbs reconnect storms. Never used for decisions.
/// </summary>
public sealed class TeamSummaryHandler(ITeamReads reads, IPresenceStore presence, ICacheStore cache, IClock clock, IOptions<CacheOptions> options)
    : IQueryHandler<TeamSummaryQuery, TeamSummaryDto>
{
    public async ValueTask<TeamSummaryDto> Handle(TeamSummaryQuery q, CancellationToken ct)
    {
        var scope = q.Actor.SeesAllTeams ? "all" : q.Actor.Id.ToString();
        return await cache.GetOrCreateAsync($"team-summary:{scope}", TimeSpan.FromSeconds(options.Value.TeamSummarySeconds),
            c => BuildAsync(q.Actor, c), ct);
    }

    private async Task<TeamSummaryDto> BuildAsync(Actor actor, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var workers = await reads.ListWorkersAsync(actor.SeesAllTeams ? null : actor.Id, ct);
        var ids = workers.Select(w => w.Id).ToList();
        // ponytail: aggregates in memory over ~5 weeks of rows; push into SQL per time zone if teams get large.
        var logs = (await reads.ListLogsSinceAsync(ids, now.AddDays(-32), ct)).ToLookup(l => l.WorkerId);
        var online = await presence.GetOnlineAsync(ids, ct);

        TeamMemberDto Member(TeamWorkerRow w)
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(w.TimeZoneId);
            var local = TimeZoneInfo.ConvertTime(now, zone);
            var (from, to) = MyMonthHandler.MonthBounds(local.Year, local.Month, zone);
            var month = logs[w.Id].Where(l => l.StartAt >= from && l.StartAt < to).ToList();
            static double Hours(IEnumerable<TeamLogRow> rows, WorkLogStatus s) =>
                Math.Round(rows.Where(l => l.Status == s).Sum(l => (l.EndAt - l.StartAt).TotalHours), 2);

            var state = w.WorkingSince is not null ? PresenceState.Working : online.Contains(w.Id) ? PresenceState.Online : PresenceState.Offline;
            // Approved is a monthly figure; pending hours share the "por decidir" count's scope (everything undecided),
            // so a card never shows "2 por decidir" next to "0 h pendientes".
            return new TeamMemberDto(w.Id, w.Name, w.TimeZoneId, state, w.WorkingSince,
                Hours(month, WorkLogStatus.Approved), Hours(logs[w.Id], WorkLogStatus.Pending),
                logs[w.Id].Count(l => l.Status == WorkLogStatus.Pending),
                logs[w.Id].Count(l => l.Status == WorkLogStatus.NeedsRevision));
        }

        List<TeamGroupDto> groups;
        if (actor.SeesAllTeams)
        {
            var supervisors = await reads.ListSupervisorsAsync(ct);
            var bySupervisor = workers.ToLookup(w => w.SupervisorId);
            groups = supervisors
                .Select(s => new TeamGroupDto(s.Id, s.Name, bySupervisor[s.Id].Select(Member).ToList()))
                .Append(new TeamGroupDto(null, null, bySupervisor[null].Select(Member).ToList()))
                .ToList();
        }
        else
        {
            groups = [new TeamGroupDto(actor.Id, null, workers.Select(Member).ToList())];
        }
        return new TeamSummaryDto(now, groups);
    }
}
