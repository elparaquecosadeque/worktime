using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Users.Events;

public sealed record WorkerUnassigned(Guid WorkerId, Guid PreviousSupervisorId) : IDomainEvent;
