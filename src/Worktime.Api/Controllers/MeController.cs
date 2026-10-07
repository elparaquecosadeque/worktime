using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Team.Queries;
using Worktime.Application.Team.Results;
using Worktime.Application.Users.Queries;
using Worktime.Application.Users.Results;

namespace Worktime.Api.Controllers;

[ApiController, Route("api"), Authorize]
public sealed class MeController(IMediator mediator) : ControllerBase
{
    [HttpGet("me")]
    public async Task<MeDto> Me(CancellationToken ct) => await mediator.Send(new GetMeQuery(User.ToActor()), ct);

    [HttpGet("supervisors")]
    public async Task<IReadOnlyList<TeamSupervisorRow>> Supervisors(CancellationToken ct) => await mediator.Send(new ListSupervisorsQuery(), ct);
}
