using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Punch.Events;

public sealed record PunchEnded(Guid WorkerId, DateTimeOffset At, IReadOnlyList<Guid> WorkLogIds) : IDomainEvent;
