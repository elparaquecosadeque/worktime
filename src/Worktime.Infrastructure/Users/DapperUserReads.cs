using Dapper;
using Microsoft.EntityFrameworkCore;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Assignments;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Users;

/// <summary>Same contract as <see cref="EfUserReads"/>, hand-written SQL.</summary>
internal sealed class DapperUserReads(WorktimeDbContext db) : IUserReads
{
    private const string SelectUsers = """
        SELECT u.id AS "Id", u.name AS "Name", u.email AS "Email", u.role AS "Role", u.is_active AS "IsActive",
               u.supervisor_id AS "SupervisorId", s.name AS "SupervisorName", u.time_zone_id AS "TimeZoneId"
        FROM users u
        LEFT JOIN users s ON s.id = u.supervisor_id
        """;

    public async Task<IReadOnlyList<UserDto>> ListAsync(Guid? supervisorId, CancellationToken ct) =>
        (await db.Database.GetDbConnection().QueryAsync<UserRow>(new CommandDefinition($"""
            {SelectUsers}
            WHERE @supervisorId::uuid IS NULL OR u.supervisor_id = @supervisorId
            ORDER BY u.role, u.name, u.id
            """, new { supervisorId }, cancellationToken: ct)))
        .Select(u => u.ToDto()).ToList();

    public async Task<UserDto?> GetAsync(Guid id, CancellationToken ct) =>
        (await db.Database.GetDbConnection().QuerySingleOrDefaultAsync<UserRow>(new CommandDefinition($"""
            {SelectUsers}
            WHERE u.id = @id
            """, new { id }, cancellationToken: ct)))
        ?.ToDto();

    public async Task<MeDto?> GetMeAsync(Guid id, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        if (await GetAsync(id, ct) is not { } user) return null;
        var connection = db.Database.GetDbConnection();
        var workingSince = await connection.QueryFirstOrDefaultAsync<DateTime?>(new CommandDefinition("""
            SELECT started_at FROM punch_sessions WHERE worker_id = @id AND ended_at IS NULL LIMIT 1
            """, new { id }, cancellationToken: ct));
        var pending = await connection.QueryFirstOrDefaultAsync<PendingRow>(new CommandDefinition("""
            SELECT id AS "Id", created_at AS "CreatedAt", note AS "Note", preferred_supervisor_id AS "PreferredSupervisorId"
            FROM assignment_requests
            WHERE worker_id = @id AND status = @pending
            LIMIT 1
            """, new { id, pending = nameof(AssignmentStatus.Pending) }, cancellationToken: ct));
        return new MeDto(user, permissions, workingSince is { } since ? new DateTimeOffset(since) : null, pending?.ToDto());
    }

    private sealed record UserRow(Guid Id, string Name, string Email, string Role, bool IsActive, Guid? SupervisorId, string? SupervisorName, string TimeZoneId)
    {
        public UserDto ToDto() => new(Id, Name, Email, Enum.Parse<Role>(Role), IsActive, SupervisorId, SupervisorName, TimeZoneId);
    }

    private sealed record PendingRow(Guid Id, DateTime CreatedAt, string? Note, Guid? PreferredSupervisorId)
    {
        public PendingRequestDto ToDto() => new(Id, new DateTimeOffset(CreatedAt), Note, PreferredSupervisorId);
    }
}
