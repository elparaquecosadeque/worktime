using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Punch.Events;

public sealed record PunchStarted(Guid WorkerId, DateTimeOffset At) : IDomainEvent;
