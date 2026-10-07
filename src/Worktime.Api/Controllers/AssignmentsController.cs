using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Assignments.Commands;
using Worktime.Application.Assignments.Queries;
using Worktime.Application.Assignments.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Api.Controllers;

[ApiController, Route("api/assignments")]
public sealed class AssignmentsController(IMediator mediator) : ControllerBase
{
    public sealed record CreateRequest(string? Note, Guid? PreferredSupervisorId);
    public sealed record FulfillRequest(Guid SupervisorId);
    public sealed record DismissRequest(string? Reason);

    [HttpGet, RequirePermission(Perms.AssignmentsResolve)]
    public async Task<IReadOnlyList<AssignmentRequestDto>> Pending(CancellationToken ct) => await mediator.Send(new ListPendingAssignmentsQuery(), ct);

    [HttpPost, RequirePermission(Perms.AssignmentsRequest)]
    public async Task<object> Create(CreateRequest b, CancellationToken ct) =>
        new { id = await mediator.Send(new RequestAssignmentCommand(User.ToActor(), b.Note, b.PreferredSupervisorId), ct) };

    [HttpPost("{id:guid}/fulfill"), RequirePermission(Perms.AssignmentsResolve)]
    public async Task<IActionResult> Fulfill(Guid id, FulfillRequest b, CancellationToken ct)
    {
        await mediator.Send(new FulfillAssignmentCommand(User.ToActor(), id, b.SupervisorId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/dismiss"), RequirePermission(Perms.AssignmentsResolve)]
    public async Task<IActionResult> Dismiss(Guid id, DismissRequest b, CancellationToken ct)
    {
        await mediator.Send(new DismissAssignmentCommand(User.ToActor(), id, b.Reason), ct);
        return NoContent();
    }
}
