using Mediator;
using Microsoft.AspNetCore.Mvc;
using Worktime.Api.Auth;
using Worktime.Application.Permissions.Commands;
using Worktime.Application.Permissions.Queries;
using Worktime.Application.Permissions.Results;
using Worktime.Domain.Permissions;
using Worktime.Domain.Users;

namespace Worktime.Api.Controllers;

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
