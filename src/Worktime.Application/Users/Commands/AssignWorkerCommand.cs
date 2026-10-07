using Mediator;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Application.Users.Commands;

/// <summary>Admin-only (re)assignment. Also closes the worker's pending assignment request, if any.</summary>
public sealed record AssignWorkerCommand(Actor Actor, Guid WorkerId, Guid SupervisorId) : ICommand<UserDto>;

public sealed class AssignWorkerHandler(IUserRepository users, IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock, IUserReads reads)
    : ICommandHandler<AssignWorkerCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(AssignWorkerCommand c, CancellationToken ct)
    {
        if (!c.Actor.Has(Perms.UsersManage)) throw new ForbiddenException("assignment.admin_only");
        var worker = await users.GetAsync(c.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        var supervisor = await users.GetAsync(c.SupervisorId, ct) ?? throw new NotFoundException("user.not_found");

        worker.AssignTo(supervisor);
        if (await requests.GetPendingForWorkerAsync(worker.Id, ct) is { } pending)
            pending.Fulfill(c.Actor.Id, clock.UtcNow);

        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(worker.Id, ct))!;
    }
}
