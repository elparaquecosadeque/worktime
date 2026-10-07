using Mediator;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Assignments.Results;

namespace Worktime.Application.Assignments.Queries;

public sealed record ListPendingAssignmentsQuery : IQuery<IReadOnlyList<AssignmentRequestDto>>;

public sealed class ListPendingAssignmentsHandler(IAssignmentReads reads) : IQueryHandler<ListPendingAssignmentsQuery, IReadOnlyList<AssignmentRequestDto>>
{
    public async ValueTask<IReadOnlyList<AssignmentRequestDto>> Handle(ListPendingAssignmentsQuery q, CancellationToken ct) =>
        await reads.ListPendingAsync(ct);
}
