using Microsoft.EntityFrameworkCore;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Team.Results;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Team;

internal sealed class TeamReads(WorktimeDbContext db) : ITeamReads
{
    public async Task<IReadOnlyList<TeamWorkerRow>> ListWorkersAsync(Guid? supervisorId, CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == Role.Worker && u.IsActive && (supervisorId == null || u.SupervisorId == supervisorId))
            .OrderBy(u => u.Name)
            .Select(u => new TeamWorkerRow(u.Id, u.Name, u.TimeZoneId, u.SupervisorId,
                db.PunchSessions.Where(p => p.WorkerId == u.Id && p.EndedAt == null).Select(p => (DateTimeOffset?)p.StartedAt).FirstOrDefault()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeamSupervisorRow>> ListSupervisorsAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Role == Role.Supervisor && u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new TeamSupervisorRow(u.Id, u.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeamLogRow>> ListLogsSinceAsync(IReadOnlyCollection<Guid> workerIds, DateTimeOffset fromUtc, CancellationToken ct) =>
        await db.WorkLogs.AsNoTracking()
            .Where(l => workerIds.Contains(l.WorkerId) && l.StartAt >= fromUtc)
            .Select(l => new TeamLogRow(l.WorkerId, l.StartAt, l.EndAt, l.Status))
            .ToListAsync(ct);
}
