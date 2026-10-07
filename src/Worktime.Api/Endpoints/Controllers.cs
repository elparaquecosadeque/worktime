using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Worktime.Api.Auth;
using Worktime.Application.Assignments.Commands;
using Worktime.Application.Assignments.Queries;
using Worktime.Application.Assignments.Results;
using Worktime.Application.Auth.Commands;
using Worktime.Application.Auth.Queries;
using Worktime.Application.Auth.Results;
using Worktime.Application.Permissions.Commands;
using Worktime.Application.Permissions.Queries;
using Worktime.Application.Permissions.Results;
using Worktime.Application.Punch.Commands;
using Worktime.Application.Punch.Results;
using Worktime.Application.Team.Queries;
using Worktime.Application.Team.Results;
using Worktime.Application.Users.Commands;
using Worktime.Application.Users.Queries;
using Worktime.Application.Users.Results;
using Worktime.Application.WorkLogs.Commands;
using Worktime.Application.WorkLogs.Queries;
using Worktime.Application.WorkLogs.Results;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;
using Worktime.Domain.WorkLogs;

namespace Worktime.Api.Endpoints;

// Controllers stay thin: translate HTTP ↔ command/query, nothing else. Rules live in Application/Domain.

[ApiController, Route("api/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    public sealed record LoginRequest(string Email, string Password);

    [HttpPost("login"), AllowAnonymous, EnableRateLimiting(RateLimits.Login)]
    public async Task<LoginResult> Login(LoginRequest body, CancellationToken ct) =>
        await mediator.Send(new LoginCommand(body.Email, body.Password), ct);

    [HttpGet("demo-accounts"), AllowAnonymous]
    public async Task<IReadOnlyList<DemoAccount>> DemoAccounts(CancellationToken ct) =>
        await mediator.Send(new ListDemoAccountsQuery(), ct);
}

[ApiController, Route("api"), Authorize]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<MeDto> Me(CancellationToken ct) => await mediator.Send(new GetMeQuery(User.ToActor()), ct);

    [HttpGet("supervisors")]
    public async Task<IReadOnlyList<TeamSupervisorRow>> Supervisors(CancellationToken ct) => await mediator.Send(new ListSupervisorsQuery(), ct);
}

[ApiController, Route("api/punch"), RequirePermission(Perms.PunchSelf)]
public sealed class PunchController(IMediator mediator) : ControllerBase
{
    [HttpPost("in")]
    public async Task<PunchStatusDto> In(CancellationToken ct) => await mediator.Send(new PunchInCommand(User.ToActor()), ct);

    [HttpPost("out")]
    public async Task<PunchStatusDto> Out(CancellationToken ct) => await mediator.Send(new PunchOutCommand(User.ToActor()), ct);
}

[ApiController, Route("api/worklogs")]
public sealed class WorkLogsController(IMediator mediator) : ControllerBase
{
    public sealed record WorkLogRequest(DateTimeOffset StartAt, DateTimeOffset EndAt, string? Note, string? Reason);
    public sealed record DecisionRequest(Decision Decision, string? Reason);
    public sealed record BatchDecisionRequest(IReadOnlyList<Guid> WorkLogIds, Decision Decision, string? Reason);

    [HttpGet("mine"), RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<IReadOnlyList<WorkLogDto>> Mine(int year, int month, CancellationToken ct) =>
        await mediator.Send(new MyMonthQuery(User.ToActor(), year, month), ct);

    [HttpPost, RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<object> Create(WorkLogRequest body, CancellationToken ct) =>
        new { id = await mediator.Send(new CreateManualWorkLogCommand(User.ToActor(), body.StartAt, body.EndAt, body.Note), ct) };

    [HttpPut("{id:guid}"), RequirePermission(Perms.WorkLogsSubmit)]
    public async Task<IActionResult> Edit(Guid id, WorkLogRequest body, CancellationToken ct)
    {
        await mediator.Send(new EditWorkLogCommand(User.ToActor(), id, body.StartAt, body.EndAt, body.Note, body.Reason), ct);
        return NoContent();
    }

    [HttpGet("inbox"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IReadOnlyList<WorkLogDto>> Inbox(bool orphans, Guid? workerId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct) =>
        await mediator.Send(new InboxQuery(User.ToActor(), orphans, workerId, from, to), ct);

    [HttpPost("{id:guid}/decision"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IActionResult> Decide(Guid id, DecisionRequest body, CancellationToken ct)
    {
        await mediator.Send(new DecideWorkLogCommand(User.ToActor(), id, body.Decision, body.Reason), ct);
        return NoContent();
    }

    /// <summary>Partial success by design: 200 with one outcome per log ("7 approved, 3 already decided").</summary>
    [HttpPost("decisions"), RequirePermission(Perms.WorkLogsApprove)]
    public async Task<IReadOnlyList<BatchItemResult>> DecideBatch(BatchDecisionRequest body, CancellationToken ct) =>
        await mediator.Send(new DecideBatchCommand(User.ToActor(), body.WorkLogIds, body.Decision, body.Reason), ct);
}

[ApiController, Route("api/team"), RequirePermission(Perms.TeamView)]
public sealed class TeamController(IMediator mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<TeamSummaryDto> Summary(CancellationToken ct) => await mediator.Send(new TeamSummaryQuery(User.ToActor()), ct);
}

[ApiController, Route("api/users"), RequirePermission(Perms.WorkersManage, Perms.UsersManage)]
public sealed class UsersController(IMediator mediator) : ControllerBase
{
    public sealed record CreateUserRequest(string Name, string Email, string Password, Role Role, string? TimeZoneId);
    public sealed record UpdateUserRequest(string Name, string Email, string TimeZoneId);
    public sealed record AssignRequest(Guid SupervisorId);

    [HttpGet]
    public async Task<IReadOnlyList<UserDto>> List(CancellationToken ct) => await mediator.Send(new ListUsersQuery(User.ToActor()), ct);

    [HttpPost]
    public async Task<UserDto> Create(CreateUserRequest b, CancellationToken ct) =>
        await mediator.Send(new CreateUserCommand(User.ToActor(), b.Name, b.Email, b.Password, b.Role, b.TimeZoneId), ct);

    [HttpPut("{id:guid}")]
    public async Task<UserDto> Update(Guid id, UpdateUserRequest b, CancellationToken ct) =>
        await mediator.Send(new UpdateUserCommand(User.ToActor(), id, b.Name, b.Email, b.TimeZoneId), ct);

    [HttpPost("{id:guid}/deactivate")]
    public async Task<UserDto> Deactivate(Guid id, CancellationToken ct) => await mediator.Send(new SetUserActiveCommand(User.ToActor(), id, false), ct);

    [HttpPost("{id:guid}/activate"), RequirePermission(Perms.UsersManage)]
    public async Task<UserDto> Activate(Guid id, CancellationToken ct) => await mediator.Send(new SetUserActiveCommand(User.ToActor(), id, true), ct);

    [HttpPost("{id:guid}/unassign")]
    public async Task<UserDto> Unassign(Guid id, CancellationToken ct) => await mediator.Send(new UnassignWorkerCommand(User.ToActor(), id), ct);

    [HttpPost("{id:guid}/assign"), RequirePermission(Perms.UsersManage)]
    public async Task<UserDto> Assign(Guid id, AssignRequest b, CancellationToken ct) =>
        await mediator.Send(new AssignWorkerCommand(User.ToActor(), id, b.SupervisorId), ct);
}

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

[ApiController, Route("api/permissions"), RequirePermission(Perms.PermissionsManage)]
public sealed class PermissionsController(IMediator mediator) : ControllerBase
{
    public sealed record Grant(Role Role, string Permission);
    public sealed record SaveRequest(IReadOnlyList<Grant> Grants);

    [HttpGet]
    public async Task<MatrixDto> Get(CancellationToken ct) => await mediator.Send(new GetMatrixQuery(), ct);

    [HttpPut]
    public async Task<SaveMatrixResult> Save(SaveRequest b, CancellationToken ct) =>
        await mediator.Send(new SaveMatrixCommand(b.Grants.Select(g => (g.Role, g.Permission)).ToList()), ct);
}

public static class RateLimits
{
    public const string Login = "login";
}
