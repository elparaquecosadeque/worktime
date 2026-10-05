using Worktime.Domain.Common;

namespace Worktime.Domain.WorkLogs;

public enum WorkLogStatus { Pending, NeedsRevision, Approved, Rejected }
public enum WorkLogSource { Punch, Manual }
public enum Decision { Approve, RequestRevision, Reject }

public sealed record WorkLogSubmitted(Guid WorkLogId, Guid WorkerId) : IDomainEvent;
public sealed record WorkLogEdited(Guid WorkLogId, Guid WorkerId) : IDomainEvent;
public sealed record WorkLogStatusChanged(Guid WorkLogId, Guid WorkerId, WorkLogStatus Status, Guid ActorId, string? Reason) : IDomainEvent;

public sealed class WorkLogEvent
{
    private WorkLogEvent() { } // EF

    internal WorkLogEvent(Guid workLogId, Guid actorId, DateTimeOffset at, WorkLogStatus? from, WorkLogStatus to, string? reason)
    {
        WorkLogId = workLogId; ActorId = actorId; At = at; From = from; To = to; Reason = reason;
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid WorkLogId { get; private set; }
    public Guid ActorId { get; private set; }
    public DateTimeOffset At { get; private set; }
    public WorkLogStatus? From { get; private set; }
    public WorkLogStatus To { get; private set; }
    public string? Reason { get; private set; }
}

public sealed class WorkLog : Entity
{
    public static readonly TimeSpan MaxManualDuration = TimeSpan.FromHours(16);
    public static readonly TimeSpan MaxManualAge = TimeSpan.FromDays(31);

    private readonly List<WorkLogEvent> _history = [];

    private WorkLog() { } // EF

    public Guid WorkerId { get; private set; }
    public DateTimeOffset StartAt { get; private set; }
    public DateTimeOffset EndAt { get; private set; }
    public WorkLogSource Source { get; private set; }
    public string? Note { get; private set; }
    public WorkLogStatus Status { get; private set; }
    public uint Version { get; private set; } // xmin
    public IReadOnlyList<WorkLogEvent> History => _history;

    public TimeSpan Duration => EndAt - StartAt;
    public bool IsDecided => Status is WorkLogStatus.Approved or WorkLogStatus.Rejected;

    /// <summary>Punch segments are already split at local midnight by <see cref="Punch.PunchSession"/>.</summary>
    public static WorkLog FromPunch(Guid workerId, DateTimeOffset start, DateTimeOffset end, DateTimeOffset now)
    {
        if (end <= start) throw new DomainException("worklog.invalid_range", "End must be after start.");
        return Submit(workerId, start, end, WorkLogSource.Punch, null, now);
    }

    public static WorkLog Manual(Guid workerId, DateTimeOffset start, DateTimeOffset end, string? note, DateTimeOffset now, TimeZoneInfo workerZone)
    {
        ValidateManualRange(start, end, now, workerZone);
        return Submit(workerId, start, end, WorkLogSource.Manual, Clean(note), now);
    }

    public void Edit(Guid actorId, DateTimeOffset start, DateTimeOffset end, string? note, string? reason, DateTimeOffset now, TimeZoneInfo workerZone)
    {
        if (actorId != WorkerId) throw new DomainException("worklog.not_owner", "Only the owner can edit a log.");
        if (Status is not (WorkLogStatus.Pending or WorkLogStatus.NeedsRevision))
            throw new DomainConflictException("worklog.not_editable", "Approved or rejected logs cannot be edited.");
        var why = Guard.RequiredReason(reason);
        ValidateManualRange(start, end, now, workerZone);

        StartAt = start.ToUniversalTime();
        EndAt = end.ToUniversalTime();
        Note = Clean(note);
        Transition(actorId, WorkLogStatus.Pending, why, now);
        Raise(new WorkLogEdited(Id, WorkerId));
    }

    public void Decide(Decision decision, Guid actorId, string? reason, DateTimeOffset now)
    {
        if (actorId == WorkerId) throw new DomainException("worklog.self_decision", "Nobody decides on their own logs.");
        if (IsDecided) throw new DomainConflictException("worklog.already_decided", "This log was already decided.");
        if (Status != WorkLogStatus.Pending) throw new DomainConflictException("worklog.not_pending", "This log is waiting for the worker.");

        var (to, why) = decision switch
        {
            Decision.Approve => (WorkLogStatus.Approved, Clean(reason)),
            Decision.RequestRevision => (WorkLogStatus.NeedsRevision, Guard.RequiredReason(reason)),
            Decision.Reject => (WorkLogStatus.Rejected, Guard.RequiredReason(reason)),
            _ => throw new ArgumentOutOfRangeException(nameof(decision)),
        };
        Transition(actorId, to, why, now);
        Raise(new WorkLogStatusChanged(Id, WorkerId, to, actorId, why));
    }

    private static WorkLog Submit(Guid workerId, DateTimeOffset start, DateTimeOffset end, WorkLogSource source, string? note, DateTimeOffset now)
    {
        var log = new WorkLog
        {
            WorkerId = workerId,
            StartAt = start.ToUniversalTime(),
            EndAt = end.ToUniversalTime(),
            Source = source,
            Note = note,
            Status = WorkLogStatus.Pending,
        };
        log._history.Add(new WorkLogEvent(log.Id, workerId, now, null, WorkLogStatus.Pending, null));
        log.Raise(new WorkLogSubmitted(log.Id, workerId));
        return log;
    }

    private void Transition(Guid actorId, WorkLogStatus to, string? reason, DateTimeOffset now)
    {
        _history.Add(new WorkLogEvent(Id, actorId, now, Status, to, reason));
        Status = to;
    }

    private static void ValidateManualRange(DateTimeOffset start, DateTimeOffset end, DateTimeOffset now, TimeZoneInfo zone)
    {
        if (end <= start) throw new DomainException("worklog.invalid_range", "End must be after start.");
        if (end - start > MaxManualDuration) throw new DomainException("worklog.too_long", "A log cannot exceed 16 hours.");
        if (end > now) throw new DomainException("worklog.in_future", "Logs cannot be in the future.");
        if (start < now - MaxManualAge) throw new DomainException("worklog.too_old", "Logs older than 31 days cannot be created.");
        if (LocalDate(start, zone) != LocalDate(end.AddTicks(-1), zone))
            throw new DomainException("worklog.crosses_midnight", "A log cannot cross local midnight.");
    }

    public static DateOnly LocalDate(DateTimeOffset at, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(at, zone).DateTime);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
