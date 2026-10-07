using Mediator;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;

namespace Worktime.Application.Assignments.Commands;

public sealed record FulfillAssignmentCommand(Actor Actor, Guid RequestId, Guid SupervisorId) : ICommand;

public sealed class FulfillAssignmentHandler(IUserRepository users, IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<FulfillAssignmentCommand>
{
    public async ValueTask<Unit> Handle(FulfillAssignmentCommand c, CancellationToken ct)
    {
        var request = await requests.GetAsync(c.RequestId, ct) ?? throw new NotFoundException("assignment.not_found");
        var worker = await users.GetAsync(request.WorkerId, ct) ?? throw new NotFoundException("user.not_found");
        var supervisor = await users.GetAsync(c.SupervisorId, ct) ?? throw new NotFoundException("user.not_found");

        worker.AssignTo(supervisor);
        request.Fulfill(c.Actor.Id, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
