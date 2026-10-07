using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Permissions.Interfaces;

public interface IRolePermissionRepository
{
    Task<IReadOnlyList<RolePermission>> GetAllAsync(CancellationToken ct);
    Task<IReadOnlyList<string>> GetForRoleAsync(Role role, CancellationToken ct);
    void ReplaceAll(IReadOnlyCollection<RolePermission> current, IReadOnlyCollection<RolePermission> next);
}
