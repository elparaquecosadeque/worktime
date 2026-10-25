using Dapper;
using Microsoft.EntityFrameworkCore;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Team.Interfaces;
using Worktime.Application.Team.Results;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Team;

/// <summary>Same contract as <see cref="EfTeamReads"/>, hand-written SQL. No EF query filter here: tenant_id is explicit.</summary>
internal sealed class DapperTeamReads(WorktimeDbContext db, ITenantContext tenant) : ITeamReads
{
    public async Task<IReadOnlyList<TeamWorkerRow>> ListWorkersAsync(Guid? supervisorId, CancellationToken ct) =>
        (await db.Database.GetDbConnection().QueryAsync<WorkerRow>(new CommandDefinition("""
            SELECT u.id AS "Id", u.name AS "Name", u.time_zone_id AS "TimeZoneId", u.supervisor_id AS "SupervisorId",
                   (SELECT p.started_at FROM punch_sessions p WHERE p.worker_id = u.id AND p.ended_at IS NULL LIMIT 1) AS "WorkingSince"
            FROM users u
            WHERE u.tenant_id = @tenantId AND u.role = @worker AND u.is_active AND (@supervisorId::uuid IS NULL OR u.supervisor_id = @supervisorId)
            ORDER BY u.name, u.id
            """, new { tenantId = tenant.Id, worker = nameof(Role.Worker), supervisorId }, cancellationToken: ct)))
        .Select(w => w.ToDto()).ToList();

    public async Task<IReadOnlyList<TeamSupervisorRow>> ListSupervisorsAsync(CancellationToken ct) =>
        (await db.Database.GetDbConnection().QueryAsync<TeamSupervisorRow>(new CommandDefinition("""
            SELECT id AS "Id", name AS "Name"
            FROM users
            WHERE tenant_id = @tenantId AND role = @supervisor AND is_active
            ORDER BY name, id
            """, new { tenantId = tenant.Id, supervisor = nameof(Role.Supervisor) }, cancellationToken: ct)))
        .ToList();

    public async Task<IReadOnlyList<TeamLogRow>> ListLogsSinceAsync(IReadOnlyCollection<Guid> workerIds, DateTimeOffset fromUtc, CancellationToken ct) =>
        (await db.Database.GetDbConnection().QueryAsync<LogRow>(new CommandDefinition("""
            SELECT worker_id AS "WorkerId", start_at AS "StartAt", end_at AS "EndAt", status AS "Status"
            FROM work_logs
            WHERE tenant_id = @tenantId AND worker_id = ANY(@workerIds) AND start_at >= @fromUtc
            """, new { tenantId = tenant.Id, workerIds = workerIds.ToArray(), fromUtc }, cancellationToken: ct)))
        .Select(l => l.ToDto()).ToList();

    private sealed record WorkerRow(Guid Id, string Name, string TimeZoneId, Guid? SupervisorId, DateTime? WorkingSince)
    {
        public TeamWorkerRow ToDto() => new(Id, Name, TimeZoneId, SupervisorId, WorkingSince is { } since ? new DateTimeOffset(since) : null);
    }

    private sealed record LogRow(Guid WorkerId, DateTime StartAt, DateTime EndAt, string Status)
    {
        public TeamLogRow ToDto() => new(WorkerId, new DateTimeOffset(StartAt), new DateTimeOffset(EndAt), Enum.Parse<WorkLogStatus>(Status));
    }
}
