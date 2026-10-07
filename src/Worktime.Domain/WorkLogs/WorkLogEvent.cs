namespace Worktime.Domain.WorkLogs;

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
