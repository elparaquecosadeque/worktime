using Mediator;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence.Results;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Application.Presence.Commands;

/// <summary>
/// Users whose replica died never disconnect cleanly; their heartbeat simply expires. Runs outside any request, so each
/// user's tenant is looked up before routing the "offline" event to that tenant's watchers.
/// </summary>
public sealed record SweepPresenceCommand : ICommand<int>;

public sealed class SweepPresenceHandler(IPresenceStore presence, IUserRepository users, ITenantContext tenant, Audience audience, IRealtimeNotifier notifier) : ICommandHandler<SweepPresenceCommand, int>
{
    public async ValueTask<int> Handle(SweepPresenceCommand c, CancellationToken ct)
    {
        var expired = await presence.SweepExpiredAsync(ct);
        foreach (var userId in expired)
        {
            if (await users.GetTenantIdAsync(userId, ct) is not { } tenantId) continue; // deleted meanwhile (demo reset)
            tenant.Use(tenantId);
            await notifier.SendAsync(await audience.WatchersOfAsync(userId, ct), RealtimeEvents.PresenceChanged, new PresenceChangedPayload(userId, false), ct);
        }
        return expired.Count;
    }
}
