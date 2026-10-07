using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;
using Worktime.Domain.WorkLogs;

namespace Worktime.Application.WorkLogs.Commands;

public sealed record CreateManualWorkLogCommand(Actor Actor, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note) : ICommand<Guid>;

public sealed class CreateManualWorkLogHandler(IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock)
    : ICommandHandler<CreateManualWorkLogCommand, Guid>
{
    public async ValueTask<Guid> Handle(CreateManualWorkLogCommand c, CancellationToken ct)
    {
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var log = WorkLog.Manual(worker.Id, c.StartAt, c.EndAt, c.Note, clock.UtcNow, worker.TimeZone);
        logs.Add(log);
        await uow.SaveChangesAsync(ct); // exclusion constraint → worklog.overlap
        return log.Id;
    }
}
