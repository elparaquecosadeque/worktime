using Worktime.Domain.Common;
using Worktime.Domain.Punch.Events;
using Worktime.Domain.WorkLogs;

namespace Worktime.Domain.Punch;

public sealed class PunchSession : Entity
{
    private PunchSession() { } // EF

    public Guid WorkerId { get; private set; }
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }

    /// <summary>"Only one open punch per worker" is guaranteed by a partial unique index, not here.</summary>
    public static PunchSession Start(Guid workerId, DateTimeOffset now)
    {
        var session = new PunchSession { WorkerId = workerId, StartedAt = now.ToUniversalTime() };
        session.Raise(new PunchStarted(workerId, session.StartedAt));
        return session;
    }

    /// <summary>Closes the session and emits one log per local day touched; sub-minute segments are dropped.</summary>
    public IReadOnlyList<WorkLog> Close(DateTimeOffset now, TimeZoneInfo workerZone)
    {
        if (EndedAt is not null) throw new DomainConflictException("punch.not_open", "This punch is already closed.");
        EndedAt = now.ToUniversalTime();

        var logs = SplitAtLocalMidnight(StartedAt, EndedAt.Value, workerZone)
            .Where(s => s.End - s.Start >= TimeSpan.FromMinutes(1))
            .Select(s => WorkLog.FromPunch(WorkerId, s.Start, s.End, now))
            .ToList();

        Raise(new PunchEnded(WorkerId, EndedAt.Value, logs.Select(l => l.Id).ToList()));
        return logs;
    }

    public static IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> SplitAtLocalMidnight(
        DateTimeOffset start, DateTimeOffset end, TimeZoneInfo zone)
    {
        var cursor = start;
        while (cursor < end)
        {
            var local = TimeZoneInfo.ConvertTime(cursor, zone);
            var nextMidnightLocal = local.Date.AddDays(1);
            var nextMidnight = new DateTimeOffset(nextMidnightLocal, zone.GetUtcOffset(nextMidnightLocal)).ToUniversalTime();
            var segmentEnd = nextMidnight < end ? nextMidnight : end;
            yield return (cursor, segmentEnd);
            cursor = segmentEnd;
        }
    }
}
