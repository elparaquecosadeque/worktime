using Microsoft.EntityFrameworkCore;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Assignments;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Users;

internal sealed class UserReads(WorktimeDbContext db) : IUserReads
{
    private IQueryable<UserDto> Project(IQueryable<User> users) =>
        users.OrderBy(u => u.Role).ThenBy(u => u.Name).Select(u => new UserDto(
            u.Id, u.Name, u.Email, u.Role, u.IsActive, u.SupervisorId,
            db.Users.Where(s => s.Id == u.SupervisorId).Select(s => s.Name).FirstOrDefault(),
            u.TimeZoneId));

    public async Task<IReadOnlyList<UserDto>> ListAsync(Guid? supervisorId, CancellationToken ct) =>
        await Project(db.Users.AsNoTracking().Where(u => supervisorId == null || u.SupervisorId == supervisorId)).ToListAsync(ct);

    public Task<UserDto?> GetAsync(Guid id, CancellationToken ct) =>
        Project(db.Users.AsNoTracking().Where(u => u.Id == id)).FirstOrDefaultAsync(ct);

    public async Task<MeDto?> GetMeAsync(Guid id, IReadOnlyList<string> permissions, CancellationToken ct)
    {
        if (await GetAsync(id, ct) is not { } user) return null;
        var workingSince = await db.PunchSessions.AsNoTracking()
            .Where(p => p.WorkerId == id && p.EndedAt == null).Select(p => (DateTimeOffset?)p.StartedAt).FirstOrDefaultAsync(ct);
        var pending = await db.AssignmentRequests.AsNoTracking()
            .Where(r => r.WorkerId == id && r.Status == AssignmentStatus.Pending)
            .Select(r => new PendingRequestDto(r.Id, r.CreatedAt, r.Note, r.PreferredSupervisorId))
            .FirstOrDefaultAsync(ct);
        return new MeDto(user, permissions, workingSince, pending);
    }
}
