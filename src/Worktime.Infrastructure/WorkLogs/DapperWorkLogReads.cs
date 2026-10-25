using Dapper;
using Microsoft.EntityFrameworkCore;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Application.WorkLogs.Results;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.WorkLogs;

/// <summary>Same contract as <see cref="EfWorkLogReads"/>, hand-written SQL. History comes in a second query (no N+1, no row explosion).
/// No EF query filter here: tenant_id is explicit in every query.</summary>
internal sealed class DapperWorkLogReads(WorktimeDbContext db, ITenantContext tenant) : IWorkLogReads
{
    private const string SelectLogs = """
        SELECT l.id AS "Id", l.worker_id AS "WorkerId", w.name AS "WorkerName", w.time_zone_id AS "WorkerTimeZoneId",
               l.start_at AS "StartAt", l.end_at AS "EndAt", l.source AS "Source", l.note AS "Note", l.status AS "Status"
        FROM work_logs l
        JOIN users w ON w.id = l.worker_id
        """;

    public async Task<IReadOnlyList<WorkLogDto>> ListForWorkerAsync(Guid workerId, DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct) =>
        await QueryAsync($"""
            {SelectLogs}
            WHERE l.tenant_id = @tenantId AND l.worker_id = @workerId AND l.start_at >= @fromUtc AND l.start_at < @toUtc
            ORDER BY l.start_at, l.id
            """, new { tenantId = tenant.Id, workerId, fromUtc, toUtc }, ct);

    public async Task<IReadOnlyList<WorkLogDto>> ListPendingAsync(Guid? supervisorId, bool orphansOnly, Guid? workerId, DateTimeOffset? fromUtc, DateTimeOffset? toUtc, CancellationToken ct) =>
        await QueryAsync($"""
            {SelectLogs}
            WHERE l.tenant_id = @tenantId AND l.status = @pending
              AND w.is_active AND w.role = @worker
              AND (@supervisorId::uuid IS NULL OR w.supervisor_id = @supervisorId)
              AND (NOT @orphansOnly OR w.supervisor_id IS NULL)
              AND (@workerId::uuid IS NULL OR w.id = @workerId)
              AND (@fromUtc::timestamptz IS NULL OR l.start_at >= @fromUtc)
              AND (@toUtc::timestamptz IS NULL OR l.start_at < @toUtc)
            ORDER BY l.start_at, l.id
            """,
            new { tenantId = tenant.Id, pending = nameof(WorkLogStatus.Pending), worker = nameof(Role.Worker), supervisorId, orphansOnly, workerId, fromUtc, toUtc }, ct);

    private async Task<IReadOnlyList<WorkLogDto>> QueryAsync(string sql, object args, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var logs = (await connection.QueryAsync<LogRow>(new CommandDefinition(sql, args, cancellationToken: ct))).ToList();
        if (logs.Count == 0) return [];

        var history = (await connection.QueryAsync<EventRow>(new CommandDefinition("""
                SELECT e.work_log_id AS "WorkLogId", e.at AS "At", e.actor_id AS "ActorId", a.name AS "ActorName",
                       e."from" AS "From", e."to" AS "To", e.reason AS "Reason"
                FROM work_log_events e
                JOIN users a ON a.id = e.actor_id
                WHERE e.tenant_id = @tenantId AND e.work_log_id = ANY(@ids)
                ORDER BY e.at, e.id
                """, new { tenantId = tenant.Id, ids = logs.Select(l => l.Id).ToArray() }, cancellationToken: ct)))
            .ToLookup(e => e.WorkLogId, e => e.ToDto());

        return logs.Select(l => l.ToDto(history[l.Id].ToList())).ToList();
    }

    private sealed record LogRow(
        Guid Id, Guid WorkerId, string WorkerName, string WorkerTimeZoneId,
        DateTime StartAt, DateTime EndAt, string Source, string? Note, string Status)
    {
        public WorkLogDto ToDto(IReadOnlyList<WorkLogEventDto> history) => new(
            Id, WorkerId, WorkerName, WorkerTimeZoneId, new DateTimeOffset(StartAt), new DateTimeOffset(EndAt),
            Enum.Parse<WorkLogSource>(Source), Note, Enum.Parse<WorkLogStatus>(Status), history);
    }

    private sealed record EventRow(Guid WorkLogId, DateTime At, Guid ActorId, string ActorName, string? From, string To, string? Reason)
    {
        public WorkLogEventDto ToDto() => new(
            new DateTimeOffset(At), ActorId, ActorName, From is null ? null : Enum.Parse<WorkLogStatus>(From), Enum.Parse<WorkLogStatus>(To), Reason);
    }
}
