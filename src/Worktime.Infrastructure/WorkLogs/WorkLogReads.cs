using Microsoft.EntityFrameworkCore;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Application.WorkLogs.Results;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.WorkLogs;

internal sealed class WorkLogReads(WorktimeDbContext db) : IWorkLogReads
{
    private IQueryable<WorkLogDto> Project(IQueryable<WorkLog> logs) =>
        from l in logs
        join w in db.Users on l.WorkerId equals w.Id
        orderby l.StartAt
        select new WorkLogDto(
            l.Id, l.WorkerId, w.Name, w.TimeZoneId, l.StartAt, l.EndAt, l.Source, l.Note, l.Status,
            l.History.OrderBy(h => h.At).Select(h => new WorkLogEventDto(
                h.At, h.ActorId, db.Users.Where(a => a.Id == h.ActorId).Select(a => a.Name).First(), h.From, h.To, h.Reason)).ToList());

    public async Task<IReadOnlyList<WorkLogDto>> ListForWorkerAsync(Guid workerId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await Project(db.WorkLogs.AsNoTracking().Where(l => l.WorkerId == workerId && l.StartAt >= fromUtc && l.StartAt < toUtc)).ToListAsync(ct);

    public async Task<IReadOnlyList<WorkLogDto>> ListPendingAsync(Guid? supervisorId, bool orphansOnly, Guid? workerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct)
    {
        var workers = db.Users.Where(u => u.IsActive && u.Role == Role.Worker);
        if (supervisorId is not null) workers = workers.Where(u => u.SupervisorId == supervisorId);
        if (orphansOnly) workers = workers.Where(u => u.SupervisorId == null);
        if (workerId is not null) workers = workers.Where(u => u.Id == workerId);

        var logs = db.WorkLogs.AsNoTracking()
            .Where(l => l.Status == WorkLogStatus.Pending && workers.Any(w => w.Id == l.WorkerId));
        if (fromUtc is not null) logs = logs.Where(l => l.StartAt >= fromUtc);
        if (toUtc is not null) logs = logs.Where(l => l.StartAt < toUtc);
        return await Project(logs).ToListAsync(ct);
    }
}
