using Mediator;
using Microsoft.Extensions.Options;
using Worktime.Application.Common;
using Worktime.Application.WorkLogs;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.Team;

public sealed class CacheOptions
{
    public const string Section = "Cache";
    public int TeamSummarySeconds { get; set; } = 5;
}

public sealed record TeamLogRow(Guid WorkerId, DateTimeOffset StartAt, DateTimeOffset EndAt, WorkLogStatus Status);

public sealed record TeamWorkerRow(Guid Id, string Name, string TimeZoneId, Guid? SupervisorId, DateTimeOffset? WorkingSince);

public sealed record TeamSupervisorRow(Guid Id, string Name);

public interface ITeamReads
{
    /// <param name="supervisorId">Only this supervisor's active workers; null = every active worker.</param>
    Task<IReadOnlyList<TeamWorkerRow>> ListWorkersAsync(Guid? supervisorId, CancellationToken ct);
    Task<IReadOnlyList<TeamSupervisorRow>> ListSupervisorsAsync(CancellationToken ct);
    Task<IReadOnlyList<TeamLogRow>> ListLogsSinceAsync(IReadOnlyCollection<Guid> workerIds, DateTimeOffset fromUtc, CancellationToken ct);
}

/// <summary>Active supervisors, for pickers (a worker suggesting one, an admin assigning).</summary>
public sealed record ListSupervisorsQuery : IQuery<IReadOnlyList<TeamSupervisorRow>>;

public sealed class ListSupervisorsHandler(ITeamReads reads) : IQueryHandler<ListSupervisorsQuery, IReadOnlyList<TeamSupervisorRow>>
{
    public async ValueTask<IReadOnlyList<TeamSupervisorRow>> Handle(ListSupervisorsQuery q, CancellationToken ct) =>
        await reads.ListSupervisorsAsync(ct);
}

public enum PresenceState { Offline, Online, Working }

public sealed record TeamMemberDto(
    Guid WorkerId, string Name, string TimeZoneId, PresenceState Presence, DateTimeOffset? WorkingSince,
    double MonthHoursApproved, double MonthHoursPending, int PendingCount, int NeedsRevisionCount);

/// <param name="SupervisorId">null = workers without a supervisor (orphans).</param>
public sealed record TeamGroupDto(Guid? SupervisorId, string? SupervisorName, IReadOnlyList<TeamMemberDto> Members);

public sealed record TeamSummaryDto(DateTimeOffset GeneratedAt, IReadOnlyList<TeamGroupDto> Groups);

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
            double Hours(WorkLogStatus s) => Math.Round(month.Where(l => l.Status == s).Sum(l => (l.EndAt - l.StartAt).TotalHours), 2);

            var state = w.WorkingSince is not null ? PresenceState.Working : online.Contains(w.Id) ? PresenceState.Online : PresenceState.Offline;
            return new TeamMemberDto(w.Id, w.Name, w.TimeZoneId, state, w.WorkingSince,
                Hours(WorkLogStatus.Approved), Hours(WorkLogStatus.Pending),
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
