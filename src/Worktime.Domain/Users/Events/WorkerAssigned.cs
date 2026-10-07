using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Users.Events;

public sealed record WorkerAssigned(Guid WorkerId, Guid SupervisorId) : IDomainEvent;
