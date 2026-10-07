using Worktime.Application.Common;
using Worktime.Application.Users.Interfaces;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Users;

/// <summary>Who may manage whom: users:manage reaches everyone, workers:manage only one's own workers.</summary>
public sealed class UserScope(IUserRepository users)
{
    public async Task<User> LoadManagedAsync(Actor actor, Guid userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct) ?? throw new NotFoundException("user.not_found");
        if (actor.Has(Perms.UsersManage)) return user;
        if (actor.Has(Perms.WorkersManage) && user.Role == Role.Worker && user.SupervisorId == actor.Id) return user;
        throw new ForbiddenException("user.not_managed");
    }
}
