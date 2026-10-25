using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Application.Presence;

/// <summary>Routes a worker-related event to everyone watching that worker: their supervisor and the admins.</summary>
public sealed class Audience(IUserRepository users, ITenantContext tenant)
{
    public async Task<IReadOnlyCollection<string>> WatchersOfAsync(Guid workerId, CancellationToken ct, bool includeWorker = false)
    {
        var groups = new List<string> { RealtimeGroups.Admins(tenant.Id) };
        var worker = await users.GetAsync(workerId, ct);
        if (worker?.SupervisorId is { } supervisorId) groups.Add(RealtimeGroups.Supervisor(supervisorId));
        if (includeWorker) groups.Add(RealtimeGroups.User(workerId));
        return groups;
    }
}
