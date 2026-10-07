using Mediator;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Application.Presence.Commands;

public sealed record HeartbeatCommand(Guid UserId) : ICommand;

public sealed class HeartbeatHandler(IPresenceStore presence) : ICommandHandler<HeartbeatCommand>
{
    public async ValueTask<Unit> Handle(HeartbeatCommand c, CancellationToken ct)
    {
        await presence.HeartbeatAsync(c.UserId, ct);
        return Unit.Value;
    }
}
