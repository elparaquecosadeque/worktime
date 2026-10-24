using Dapper;
using Microsoft.EntityFrameworkCore;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Assignments.Results;
using Worktime.Domain.Assignments;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Assignments;

/// <summary>Same contract as <see cref="EfAssignmentReads"/>, hand-written SQL.</summary>
internal sealed class DapperAssignmentReads(WorktimeDbContext db) : IAssignmentReads
{
    public async Task<IReadOnlyList<AssignmentRequestDto>> ListPendingAsync(CancellationToken ct) =>
        (await db.Database.GetDbConnection().QueryAsync<RequestRow>(new CommandDefinition("""
            SELECT r.id AS "Id", r.worker_id AS "WorkerId", w.name AS "WorkerName", r.note AS "Note",
                   r.preferred_supervisor_id AS "PreferredSupervisorId", s.name AS "PreferredSupervisorName", r.created_at AS "CreatedAt"
            FROM assignment_requests r
            JOIN users w ON w.id = r.worker_id
            LEFT JOIN users s ON s.id = r.preferred_supervisor_id
            WHERE r.status = @pending
            ORDER BY r.created_at, r.id
            """, new { pending = nameof(AssignmentStatus.Pending) }, cancellationToken: ct)))
        .Select(r => r.ToDto()).ToList();

    private sealed record RequestRow(
        Guid Id, Guid WorkerId, string WorkerName, string? Note, Guid? PreferredSupervisorId, string? PreferredSupervisorName, DateTime CreatedAt)
    {
        public AssignmentRequestDto ToDto() => new(Id, WorkerId, WorkerName, Note, PreferredSupervisorId, PreferredSupervisorName, new DateTimeOffset(CreatedAt));
    }
}
