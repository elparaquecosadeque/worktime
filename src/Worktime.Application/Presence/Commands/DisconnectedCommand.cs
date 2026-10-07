using Mediator;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence.Results;
using Worktime.Domain.Users;

namespace Worktime.Application.Presence.Commands;

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
