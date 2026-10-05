using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Application.Common;

/// <summary>Who is calling, as proven by the validated token.</summary>
public sealed record Actor(Guid Id, Role Role, IReadOnlySet<string> Permissions)
{
    public bool Has(string permission) => Permissions.Contains(permission);

    /// <summary>Global scope over every team (admin by default, but it is the matrix that decides).</summary>
    public bool SeesAllTeams => Has(Perms.MonitorAll);

    /// <summary>Ownership rule shared by approvals, user management and team views.</summary>
    public bool Oversees(User worker) => SeesAllTeams || worker.SupervisorId == Id;
}
