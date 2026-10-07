using Worktime.Application.Common;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Application.Presence;

/// <summary>Routes a worker-related event to everyone watching that worker: their supervisor and the admins.</summary>
public sealed class Audience(IUserRepository users)
{
    public async Task<IReadOnlyCollection<string>> WatchersOfAsync(Guid workerId, CancellationToken ct, bool includeWorker = false)
    {
        var groups = new List<string> { RealtimeGroups.Admins };
        var worker = await users.GetAsync(workerId, ct);
        if (worker?.SupervisorId is { } supervisorId) groups.Add(RealtimeGroups.Supervisor(supervisorId));
        if (includeWorker) groups.Add(RealtimeGroups.User(workerId));
        return groups;
    }
}
