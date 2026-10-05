using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Users;
using Worktime.Domain.Users;

namespace Worktime.Application.Presence;

public static class RealtimeEvents
{
    public const string PresenceChanged = "PresenceChanged";
    public const string PunchChanged = "PunchChanged";
    public const string WorkLogChanged = "WorkLogChanged";
    public const string WorkLogStatusChanged = "WorkLogStatusChanged";
    public const string AssignmentChanged = "AssignmentChanged";
    public const string AssignmentRequested = "AssignmentRequested";
    public const string AssignmentResolved = "AssignmentResolved";
    public const string ForceLogout = "ForceLogout";
    public const string DemoReset = "DemoReset";
}

public sealed record PresenceChangedPayload(Guid WorkerId, bool Online);

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

public sealed record ConnectedCommand(Guid UserId, Role Role) : ICommand;

public sealed class ConnectedHandler(IPresenceStore presence, Audience audience, IRealtimeNotifier notifier) : ICommandHandler<ConnectedCommand>
{
    public async ValueTask<Unit> Handle(ConnectedCommand c, CancellationToken ct)
    {
        if (await presence.ConnectAsync(c.UserId, ct) && c.Role == Role.Worker)
            await notifier.SendAsync(await audience.WatchersOfAsync(c.UserId, ct), RealtimeEvents.PresenceChanged, new PresenceChangedPayload(c.UserId, true), ct);
        return Unit.Value;
    }
}

public sealed record DisconnectedCommand(Guid UserId, Role Role) : ICommand;

public sealed class DisconnectedHandler(IPresenceStore presence, Audience audience, IRealtimeNotifier notifier) : ICommandHandler<DisconnectedCommand>
{
    public async ValueTask<Unit> Handle(DisconnectedCommand c, CancellationToken ct)
    {
        if (await presence.DisconnectAsync(c.UserId, ct) && c.Role == Role.Worker)
            await notifier.SendAsync(await audience.WatchersOfAsync(c.UserId, ct), RealtimeEvents.PresenceChanged, new PresenceChangedPayload(c.UserId, false), ct);
        return Unit.Value;
    }
}

public sealed record HeartbeatCommand(Guid UserId) : ICommand;

public sealed class HeartbeatHandler(IPresenceStore presence) : ICommandHandler<HeartbeatCommand>
{
    public async ValueTask<Unit> Handle(HeartbeatCommand c, CancellationToken ct)
    {
        await presence.HeartbeatAsync(c.UserId, ct);
        return Unit.Value;
    }
}

/// <summary>Users whose replica died never disconnect cleanly; their heartbeat simply expires.</summary>
public sealed record SweepPresenceCommand : ICommand<int>;

public sealed class SweepPresenceHandler(IPresenceStore presence, Audience audience, IRealtimeNotifier notifier) : ICommandHandler<SweepPresenceCommand, int>
{
    public async ValueTask<int> Handle(SweepPresenceCommand c, CancellationToken ct)
    {
        var expired = await presence.SweepExpiredAsync(ct);
        foreach (var userId in expired)
            await notifier.SendAsync(await audience.WatchersOfAsync(userId, ct), RealtimeEvents.PresenceChanged, new PresenceChangedPayload(userId, false), ct);
        return expired.Count;
    }
}
