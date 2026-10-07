using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.WorkLogs.Interfaces;

namespace Worktime.Application.WorkLogs.Commands;

public sealed record EditWorkLogCommand(Actor Actor, Guid WorkLogId, DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note, string? Reason) : ICommand;

public sealed class EditWorkLogHandler(IUserRepository users, IWorkLogRepository logs, IUnitOfWork uow, IClock clock, KeyedLock locks)
    : ICommandHandler<EditWorkLogCommand>
{
    public async ValueTask<Unit> Handle(EditWorkLogCommand c, CancellationToken ct)
    {
        using var _ = await locks.AcquireAsync(c.WorkLogId, ct);
        var log = await logs.GetAsync(c.WorkLogId, ct) ?? throw new NotFoundException("worklog.not_found");
        var worker = await users.GetAsync(log.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        log.Edit(c.Actor.Id, c.StartAt, c.EndAt, c.Note, c.Reason, clock.UtcNow, worker.TimeZone);
        await uow.SaveChangesAsync(ct); // a supervisor deciding meanwhile → xmin conflict
        return Unit.Value;
    }
}
