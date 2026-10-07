using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Team.Queries;
using Worktime.Application.Team.Results;
using Worktime.Domain.Permissions;

namespace Worktime.Api.Controllers;

[ApiController, Route("api/team"), RequirePermission(Perms.TeamView)]
public sealed class TeamController(IMediator mediator) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<TeamSummaryDto> Summary(CancellationToken ct) => await mediator.Send(new TeamSummaryQuery(User.ToActor()), ct);
}
