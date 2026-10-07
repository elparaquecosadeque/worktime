using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.WorkLogs.Events;

public sealed record WorkLogStatusChanged(Guid WorkLogId, Guid WorkerId, WorkLogStatus Status, Guid ActorId, string? Reason) : IDomainEvent;
