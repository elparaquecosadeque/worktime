using Microsoft.EntityFrameworkCore;
using Worktime.Application.Permissions.Interfaces;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;
using Worktime.Infrastructure.Persistence;

namespace Worktime.Infrastructure.Permissions;

internal sealed class RolePermissionRepository(WorktimeDbContext db) : IRolePermissionRepository
{
    public async Task<IReadOnlyList<RolePermission>> GetAllAsync(CancellationToken ct) => await db.RolePermissions.ToListAsync(ct);

    public async Task<IReadOnlyList<string>> GetForRoleAsync(Role role, CancellationToken ct) =>
        await db.RolePermissions.AsNoTracking().Where(e => e.Role == role).Select(e => e.Permission).OrderBy(p => p).ToListAsync(ct);

    public void ReplaceAll(IReadOnlyCollection<RolePermission> current, IReadOnlyCollection<RolePermission> next)
    {
        // Diff instead of delete-all/insert-all: EF cannot track a removed and an added row with the same key.
        static (Role, string) Key(RolePermission e) => (e.Role, e.Permission);
        var nextKeys = next.Select(Key).ToHashSet();
        var currentKeys = current.Select(Key).ToHashSet();
        db.RolePermissions.RemoveRange(current.Where(e => !nextKeys.Contains(Key(e))));
        db.RolePermissions.AddRange(next.Where(e => !currentKeys.Contains(Key(e))));
    }
}
