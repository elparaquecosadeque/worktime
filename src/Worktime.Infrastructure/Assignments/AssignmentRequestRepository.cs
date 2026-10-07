using Microsoft.EntityFrameworkCore;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Domain.Assignments;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Assignments;

internal sealed class AssignmentRequestRepository(WorktimeDbContext db) : IAssignmentRequestRepository
{
    public async Task<AssignmentRequest?> GetAsync(Guid id, CancellationToken ct) => await db.AssignmentRequests.FindAsync([id], ct);

    public Task<AssignmentRequest?> GetPendingForWorkerAsync(Guid workerId, CancellationToken ct) =>
        db.AssignmentRequests.SingleOrDefaultAsync(r => r.WorkerId == workerId && r.Status == AssignmentStatus.Pending, ct);

    public void Add(AssignmentRequest request) => db.AssignmentRequests.Add(request);
}
