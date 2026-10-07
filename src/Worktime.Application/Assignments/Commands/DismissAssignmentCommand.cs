using Mediator;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;

namespace Worktime.Application.Assignments.Commands;

public sealed record DismissAssignmentCommand(Actor Actor, Guid RequestId, string? Reason) : ICommand;

public sealed class DismissAssignmentHandler(IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<DismissAssignmentCommand>
{
    public async ValueTask<Unit> Handle(DismissAssignmentCommand c, CancellationToken ct)
    {
        var request = await requests.GetAsync(c.RequestId, ct) ?? throw new NotFoundException("assignment.not_found");
        request.Dismiss(c.Actor.Id, c.Reason, clock.UtcNow);
        await uow.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
