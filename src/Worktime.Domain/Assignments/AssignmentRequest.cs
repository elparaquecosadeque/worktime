using Worktime.Domain.Assignments.Events;
using Worktime.Domain.Common;
using Worktime.Domain.Users;

namespace Worktime.Domain.Assignments;

public sealed class AssignmentRequest : Entity
{
    private AssignmentRequest() { } // EF

    public Guid WorkerId { get; private set; }
    public string? Note { get; private set; }
    public Guid? PreferredSupervisorId { get; private set; }
    public AssignmentStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid? ResolvedById { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public string? ResolutionReason { get; private set; }

    /// <summary>"One pending request per worker" is guaranteed by a partial unique index.</summary>
    public static AssignmentRequest Create(User worker, string? note, Guid? preferredSupervisorId, DateTimeOffset now)
    {
        if (worker.Role != Role.Worker) throw new DomainException("assignment.not_a_worker", "Only workers can request a supervisor.");
        if (worker.SupervisorId is not null) throw new DomainConflictException("assignment.already_assigned", "You already have a supervisor.");

        var request = new AssignmentRequest
        {
            WorkerId = worker.Id,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            PreferredSupervisorId = preferredSupervisorId,
            Status = AssignmentStatus.Pending,
            CreatedAt = now,
        };
        request.Raise(new AssignmentRequested(request.Id, worker.Id));
        return request;
    }

    public void Fulfill(Guid adminId, DateTimeOffset now) => Resolve(AssignmentStatus.Fulfilled, adminId, null, now);

    public void Dismiss(Guid adminId, string? reason, DateTimeOffset now) =>
        Resolve(AssignmentStatus.Dismissed, adminId, Guard.RequiredReason(reason), now);

    private void Resolve(AssignmentStatus status, Guid adminId, string? reason, DateTimeOffset now)
    {
        if (Status != AssignmentStatus.Pending) throw new DomainConflictException("assignment.already_resolved", "This request was already resolved.");
        Status = status;
        ResolvedById = adminId;
        ResolvedAt = now;
        ResolutionReason = reason;
        Raise(new AssignmentResolved(Id, WorkerId, status, reason));
    }
}
