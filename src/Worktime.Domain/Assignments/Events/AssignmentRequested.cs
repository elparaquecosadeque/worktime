using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Assignments.Events;

public sealed record AssignmentRequested(Guid RequestId, Guid WorkerId) : IDomainEvent;
