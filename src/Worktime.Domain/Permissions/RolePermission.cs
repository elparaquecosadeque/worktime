using Worktime.Domain.Common;
using Worktime.Domain.Users;

namespace Worktime.Domain.Permissions;

public sealed class RolePermission
{
    private RolePermission() { } // EF

    public RolePermission(Role role, string permission)
    {
        if (!Perms.All.Contains(permission)) throw new DomainException("permission.unknown", $"Unknown permission '{permission}'.");
        Role = role;
        Permission = permission;
    }

    public Role Role { get; private set; }
    public string Permission { get; private set; } = "";
}
