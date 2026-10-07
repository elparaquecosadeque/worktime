using Worktime.Domain.Assignments;

namespace Worktime.Application.Assignments.Interfaces;

public interface IAssignmentRequestRepository
{
    Task<AssignmentRequest?> GetAsync(Guid id, CancellationToken ct);
    Task<AssignmentRequest?> GetPendingForWorkerAsync(Guid workerId, CancellationToken ct);
    void Add(AssignmentRequest request);
}
