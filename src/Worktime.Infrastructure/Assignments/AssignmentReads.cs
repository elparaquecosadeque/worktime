using Microsoft.EntityFrameworkCore;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Assignments.Results;
using Worktime.Domain.Assignments;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Assignments;

internal sealed class AssignmentReads(WorktimeDbContext db) : IAssignmentReads
{
    public async Task<IReadOnlyList<AssignmentRequestDto>> ListPendingAsync(CancellationToken ct) =>
        await (from r in db.AssignmentRequests.AsNoTracking()
               join w in db.Users on r.WorkerId equals w.Id
               where r.Status == AssignmentStatus.Pending
               orderby r.CreatedAt
               select new AssignmentRequestDto(r.Id, r.WorkerId, w.Name, r.Note, r.PreferredSupervisorId,
                   db.Users.Where(s => s.Id == r.PreferredSupervisorId).Select(s => s.Name).FirstOrDefault(), r.CreatedAt))
            .ToListAsync(ct);
}
