using Microsoft.EntityFrameworkCore;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.WorkLogs;

internal sealed class WorkLogRepository(WorktimeDbContext db) : IWorkLogRepository
{
    public Task<WorkLog?> GetAsync(Guid id, CancellationToken ct) =>
        db.WorkLogs.Include(l => l.History).SingleOrDefaultAsync(l => l.Id == id, ct);

    public void Add(WorkLog log) => db.WorkLogs.Add(log);
}
