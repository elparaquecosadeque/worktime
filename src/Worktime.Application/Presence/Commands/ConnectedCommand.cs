using Mediator;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Presence.Results;
using Worktime.Domain.Users;

namespace Worktime.Application.Presence.Commands;

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
