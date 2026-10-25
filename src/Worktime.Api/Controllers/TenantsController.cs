using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Worktime.Api.Middleware;
using Worktime.Application.Tenants.Queries;
using Worktime.Domain.Tenants;

namespace Worktime.Api.Controllers;

/// <summary>Public: the SPA asks which tenant its host is, and the space picker turns a slug into that tenant's host.</summary>
[ApiController, Route("api"), AllowAnonymous]
public sealed class TenantsController(IMediator mediator, IOptions<TenancyOptions> tenancy) : ControllerBase
{
    public sealed record CurrentTenantResponse(string Name, string Slug, TenantStatus Status, bool IsDemo, string RootHost);
    public sealed record TenantHostResponse(string Name, string Slug, string Host);

    [HttpGet("tenant")]
    public async Task<CurrentTenantResponse> Current(CancellationToken ct)
    {
        var t = await mediator.Send(new CurrentTenantQuery(), ct);
        return new(t.Name, t.Slug, t.Status, t.IsDemo, tenancy.Value.RootHost);
    }

    [HttpGet("tenants/{slug}")]
    public async Task<TenantHostResponse> Find(string slug, CancellationToken ct)
    {
        var t = await mediator.Send(new FindTenantQuery(slug), ct);
        return new(t.Name, t.Slug, tenancy.Value.HostOf(t.Slug));
    }
}
