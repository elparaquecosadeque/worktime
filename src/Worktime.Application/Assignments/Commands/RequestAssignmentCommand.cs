using Mediator;
using Worktime.Application.Assignments.Interfaces;
using Worktime.Application.Common;
using Worktime.Application.Common.Interfaces;
using Worktime.Application.Users.Interfaces;
using Worktime.Domain.Assignments;

namespace Worktime.Application.Assignments.Commands;

/// <summary>A second pending request loses on the partial unique index → assignment.already_pending (409).</summary>
public sealed record RequestAssignmentCommand(Actor Actor, string? Note, Guid? PreferredSupervisorId) : ICommand<Guid>;

public sealed class RequestAssignmentHandler(IUserRepository users, IAssignmentRequestRepository requests, IUnitOfWork uow, IClock clock)
    : ICommandHandler<RequestAssignmentCommand, Guid>
{
    public async ValueTask<Guid> Handle(RequestAssignmentCommand c, CancellationToken ct)
    {
        var worker = await users.GetAsync(c.Actor.Id, ct) ?? throw new NotFoundException("user.not_found");
        var request = AssignmentRequest.Create(worker, c.Note, c.PreferredSupervisorId, clock.UtcNow);
        requests.Add(request);
        await uow.SaveChangesAsync(ct);
        return request.Id;
    }
}
