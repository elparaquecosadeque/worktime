using Mediator;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Application.Users.Results;

namespace Worktime.Application.Users.Commands;

/// <summary>
/// The supervisor lets a worker go (or an admin detaches them). The xmin token on the user row plus the
/// FOR SHARE read in approvals make "approve while being unassigned" resolve to exactly one order.
/// </summary>
public sealed record UnassignWorkerCommand(Actor Actor, Guid WorkerId) : ICommand<UserDto>;

public sealed class UnassignWorkerHandler(UserScope scope, IUnitOfWork uow, IUserReads reads) : ICommandHandler<UnassignWorkerCommand, UserDto>
{
    public async ValueTask<UserDto> Handle(UnassignWorkerCommand c, CancellationToken ct)
    {
        var worker = await scope.LoadManagedAsync(c.Actor, c.WorkerId, ct);
        worker.Unassign();
        await uow.SaveChangesAsync(ct);
        return (await reads.GetAsync(worker.Id, ct))!;
    }
}
