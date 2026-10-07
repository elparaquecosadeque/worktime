using Mediator;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Permissions.Interfaces;
using Worktime.Application.Permissions.Results;
using Worktime.Application.Users.Interfaces;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Commands;

/// <param name="Grants">The full desired matrix; anything missing is revoked.</param>
public sealed record SaveMatrixCommand(IReadOnlyList<(Role Role, string Permission)> Grants) : ICommand<SaveMatrixResult>;

/// <summary>
/// Permissions travel inside the JWT, so a change only bites after re-login. Rotating the stamp of every
/// user in a changed role forces that re-login immediately (and their live sessions get ForceLogout).
/// </summary>
public sealed class SaveMatrixHandler(IRolePermissionRepository matrix, IUserRepository users, IUnitOfWork uow)
    : ICommandHandler<SaveMatrixCommand, SaveMatrixResult>
{
    public async ValueTask<SaveMatrixResult> Handle(SaveMatrixCommand c, CancellationToken ct)
    {
        var next = c.Grants.Distinct().Select(g => new RolePermission(g.Role, g.Permission)).ToList();
        var current = await matrix.GetAllAsync(ct);
        var changed = PermissionMatrix.Diff(current, next);
        if (changed.Count == 0) return new SaveMatrixResult([], 0);

        matrix.ReplaceAll(current, next);
        var affected = (await users.ListByRolesAsync(changed.ToList(), ct)).Where(u => u.IsActive).ToList();
        foreach (var user in affected) user.RotateStamp("permissions.changed");

        await uow.SaveChangesAsync(ct);
        return new SaveMatrixResult(changed.Order().ToList(), affected.Count);
    }
}
