using Mediator;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence.Results;

namespace Worktime.Application.Presence.Commands;

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
