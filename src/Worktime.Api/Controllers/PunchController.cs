using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Punch.Commands;
using Worktime.Application.Punch.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Api.Controllers;

[ApiController, Route("api/punch"), RequirePermission(Perms.PunchSelf)]
public sealed class PunchController(IMediator mediator) : ControllerBase
{
    [HttpPost("in")]
    public async Task<PunchStatusDto> In(CancellationToken ct) => await mediator.Send(new PunchInCommand(User.ToActor()), ct);

    [HttpPost("out")]
    public async Task<PunchStatusDto> Out(CancellationToken ct) => await mediator.Send(new PunchOutCommand(User.ToActor()), ct);
}
