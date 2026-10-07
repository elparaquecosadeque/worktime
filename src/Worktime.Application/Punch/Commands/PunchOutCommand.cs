using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Punch.Interfaces;
using Worktime.Application.Punch.Results;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;

namespace Worktime.Application.Punch.Commands;

public sealed record PunchOutCommand(Actor Actor) : ICommand<PunchStatusDto>;

public sealed class PunchOutHandler(IPunchRepository punches, IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock)
    : ICommandHandler<PunchOutCommand, PunchStatusDto>
{
    public async ValueTask<PunchStatusDto> Handle(PunchOutCommand c, CancellationToken ct)
    {
        var session = await punches.GetOpenAsync(c.Actor.Id, ct) ?? throw new Domain.Common.DomainConflictException("punch.not_open", "No open punch.");
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");

        var created = session.Close(clock.UtcNow, worker.TimeZone);
        foreach (var log in created) logs.Add(log);
        await uow.SaveChangesAsync(ct); // overlap with a manual log → worklog.overlap (409)
        return new PunchStatusDto(null, created.Select(l => l.Id).ToList());
    }
}
