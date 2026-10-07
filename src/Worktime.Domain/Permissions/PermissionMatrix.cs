using Worktime.Domain.Common;
using Worktime.Domain.Users;

namespace Worktime.Domain.Permissions;

public static class PermissionMatrix
{
    public static readonly IReadOnlyDictionary<Role, string[]> Defaults = new Dictionary<Role, string[]>
    {
        [Role.Worker] = [Perms.PunchSelf, Perms.WorkLogsSubmit, Perms.AssignmentsRequest],
        [Role.Supervisor] = [Perms.WorkLogsApprove, Perms.WorkersManage, Perms.TeamView],
        [Role.Admin] = [Perms.WorkLogsApprove, Perms.UsersManage, Perms.AssignmentsResolve, Perms.TeamView, Perms.MonitorAll, Perms.PermissionsManage],
    };

    /// <summary>Cells nobody can switch off, so the admin can never lock everyone out of the matrix or the users.</summary>
    public static readonly IReadOnlySet<(Role Role, string Permission)> Locked = new HashSet<(Role, string)>
    {
        (Role.Admin, Perms.PermissionsManage),
        (Role.Admin, Perms.UsersManage),
    };

    public static IEnumerable<RolePermission> DefaultEntries() =>
        Defaults.SelectMany(kv => kv.Value.Select(p => new RolePermission(kv.Key, p)));

    /// <summary>Validates a full replacement matrix and returns the roles whose set changed.</summary>
    public static IReadOnlySet<Role> Diff(IReadOnlyCollection<RolePermission> current, IReadOnlyCollection<RolePermission> next)
    {
        foreach (var locked in Locked)
            if (!next.Any(e => e.Role == locked.Role && e.Permission == locked.Permission))
                throw new DomainException("permission.locked", $"{locked.Role}/{locked.Permission} cannot be removed.");

        return Enum.GetValues<Role>()
            .Where(r => !Set(current, r).SetEquals(Set(next, r)))
            .ToHashSet();

        static HashSet<string> Set(IEnumerable<RolePermission> entries, Role role) =>
            entries.Where(e => e.Role == role).Select(e => e.Permission).ToHashSet();
    }
}
