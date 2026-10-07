using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Worktime.Application.Auth.Commands;
using Worktime.Application.Auth.Queries;
using Worktime.Application.Auth.Results;

namespace Worktime.Api.Controllers;

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
