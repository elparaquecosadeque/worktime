using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Permissions;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetForRoleAsync(Role role, CancellationToken ct);
    void ReplaceAll(IReadOnlyCollection<RolePermission> current, IReadOnlyCollection<RolePermission> next);
}

public sealed record MatrixCell(Role Role, string Permission, bool Granted, bool Locked);

/// <param name="ActiveUsersByRole">Lets the client warn "saving forces N users to sign in again" before saving.</param>
public sealed record MatrixDto(IReadOnlyList<string> Permissions, IReadOnlyList<MatrixCell> Cells, IReadOnlyDictionary<Role, int> ActiveUsersByRole);

public sealed record GetMatrixQuery : IQuery<MatrixDto>;

public sealed class GetMatrixHandler(IRolePermissionRepository matrix, IUserRepository users) : IQueryHandler<GetMatrixQuery, MatrixDto>
{
    public async ValueTask<MatrixDto> Handle(GetMatrixQuery q, CancellationToken ct)
    {
        var granted = (await matrix.GetAllAsync(ct)).Select(e => (e.Role, e.Permission)).ToHashSet();
        var cells = Enum.GetValues<Role>()
            .SelectMany(r => Perms.All.Select(p => new MatrixCell(r, p, granted.Contains((r, p)), PermissionMatrix.Locked.Contains((r, p)))))
            .ToList();
        var counts = (await users.ListByRolesAsync(Enum.GetValues<Role>(), ct))
            .Where(u => u.IsActive)
            .GroupBy(u => u.Role)
            .ToDictionary(g => g.Key, g => g.Count());
        return new MatrixDto(Perms.All, cells, counts);
    }
}

/// <param name="Grants">The full desired matrix; anything missing is revoked.</param>
public sealed record SaveMatrixCommand(IReadOnlyList<(Role Role, string Permission)> Grants) : ICommand<SaveMatrixResult>;

public sealed record SaveMatrixResult(IReadOnlyList<Role> ChangedRoles, int UsersSignedOut);

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
