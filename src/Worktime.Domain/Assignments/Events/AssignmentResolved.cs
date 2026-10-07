using Worktime.Domain.Common.Interfaces;

namespace Worktime.Domain.Assignments.Events;

public sealed record AssignmentResolved(Guid RequestId, Guid WorkerId, AssignmentStatus Status, string? Reason) : IDomainEvent;
