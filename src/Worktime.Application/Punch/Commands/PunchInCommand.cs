using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Punch.Interfaces;
using Worktime.Application.Punch.Results;
using Worktime.Domain.Punch;

namespace Worktime.Application.Punch.Commands;

/// <summary>A double click or a second tab loses on the partial unique index → punch.already_open (409).</summary>
public sealed record PunchInCommand(Actor Actor) : ICommand<PunchStatusDto>;

public sealed class PunchInHandler(IPunchRepository punches, IUnitOfWork uow, IClock clock) : ICommandHandler<PunchInCommand, PunchStatusDto>
{
    public async ValueTask<PunchStatusDto> Handle(PunchInCommand c, CancellationToken ct)
    {
        var session = PunchSession.Start(c.Actor.Id, clock.UtcNow);
        punches.Add(session);
        await uow.SaveChangesAsync(ct);
        return new PunchStatusDto(session.StartedAt, []);
    }
}
