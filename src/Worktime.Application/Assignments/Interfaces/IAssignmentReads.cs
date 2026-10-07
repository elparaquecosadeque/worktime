using Worktime.Application.Assignments.Results;

namespace Worktime.Application.Assignments.Interfaces;

public interface IAssignmentReads
{
    Task<IReadOnlyList<AssignmentRequestDto>> ListPendingAsync(CancellationToken ct);
}
