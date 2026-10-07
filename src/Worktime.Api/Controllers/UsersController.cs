using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Users.Commands;
using Worktime.Application.Users.Queries;
using Worktime.Application.Users.Results;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Api.Controllers;

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
